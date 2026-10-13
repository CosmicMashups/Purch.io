using Microsoft.Extensions.Caching.Memory;
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
    /// <summary>How long a device's session state is trusted before it is read again. Bounds how late a
    /// revoke / pairing reset is enforced (per instance); displays poll every few seconds, so without this
    /// every poll, including 304s, paid a database round trip here.</summary>
    internal static readonly TimeSpan SessionStateTtl = TimeSpan.FromSeconds(10);

    public async Task InvokeAsync(HttpContext context, IDeviceRepository deviceRepository, IMemoryCache? cache = null)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var tenantIdClaim = context.User.FindFirst(JwtClaimTypes.TenantId)?.Value;
            if (!Guid.TryParse(tenantIdClaim, out var tenantId))
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
                DeviceSessionState? device;
                if (cache is null)
                {
                    device = await deviceRepository.GetSessionStateAsync(deviceId, context.RequestAborted);
                }
                else
                {
                    // Tenant is part of the key so a token can never read another tenant's cached state.
                    var key = DeviceSessionCacheKey.For(tenantId, deviceId);
                    if (!cache.TryGetValue(key, out device))
                    {
                        device = await deviceRepository.GetSessionStateAsync(deviceId, context.RequestAborted);
                        // Only a found device is cached: an unknown id must be re-checked, not remembered.
                        if (device is not null)
                            cache.Set(key, device, SessionStateTtl);
                    }
                }

                if (device is null || device.Status is Purch.Domain.Enums.DeviceStatus.Revoked or Purch.Domain.Enums.DeviceStatus.Inactive || device.IsDeleted || !hasSessionVersion || device.SessionVersion != tokenSessionVersion)
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
