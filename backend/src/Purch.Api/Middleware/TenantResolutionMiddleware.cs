using Purch.Application.Auth;

namespace Purch.Api.Middleware;

/// <summary>
/// Fail-closed check: any authenticated request must carry a valid tenant_id claim,
/// and — if it carries a device_id claim (Register/Kiosk/OrderBoard/KitchenDisplay
/// tokens all do) — its device_session_ver claim must match the device's current
/// SessionVersion. Runs after authentication, before endpoint execution. Actual
/// per-query tenant enforcement happens via PurchDbContext's global query filter
/// (fed by ICurrentTenantProvider) — this middleware exists to reject a malformed/
/// missing claim, or a token orphaned by a pairing reset, early with a clear 401,
/// rather than let a query filter silently resolve to "no tenant" further down or
/// let a revoked device session keep working until its access token expires.
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IDeviceRepository deviceRepository)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var tenantIdClaim = context.User.FindFirst(JwtClaimTypes.TenantId)?.Value;
            if (!Guid.TryParse(tenantIdClaim, out _))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Missing or invalid tenant claim.");
                return;
            }

            var deviceIdClaim = context.User.FindFirst(JwtClaimTypes.DeviceId)?.Value;
            if (Guid.TryParse(deviceIdClaim, out var deviceId))
            {
                // A missing or unreadable version must not default to 0 and match a device that has
                // never been reset: it is rejected like any other stale session.
                var hasSessionVersion = int.TryParse(context.User.FindFirst(JwtClaimTypes.DeviceSessionVersion)?.Value, out var tokenSessionVersion);
                var device = await deviceRepository.GetByIdAsync(deviceId, context.RequestAborted);

                if (device is null || !hasSessionVersion || device.SessionVersion != tokenSessionVersion)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsync("Device session has been reset.");
                    return;
                }
            }
        }

        await next(context);
    }
}
