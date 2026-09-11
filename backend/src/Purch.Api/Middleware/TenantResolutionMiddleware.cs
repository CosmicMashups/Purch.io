using Purch.Infrastructure.Auth;

namespace Purch.Api.Middleware;

/// <summary>
/// Fail-closed check: any authenticated request must carry a valid tenant_id claim.
/// Runs after authentication, before endpoint execution. Actual per-query enforcement
/// happens via PurchDbContext's global query filter (fed by ICurrentTenantProvider) —
/// this middleware exists to reject a malformed/missing claim early with a clear 401,
/// rather than let a query filter silently resolve to "no tenant" further down.
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
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
        }

        await next(context);
    }
}
