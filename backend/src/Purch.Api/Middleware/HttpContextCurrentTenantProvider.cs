using Purch.Application.Auth;
using Purch.Application.Common;

namespace Purch.Api.Middleware;

/// <summary>Reads the tenant id claim set on HttpContext.User by JWT auth — scoped per request.</summary>
public sealed class HttpContextCurrentTenantProvider(IHttpContextAccessor httpContextAccessor) : ICurrentTenantProvider
{
    public Guid? TenantId
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User.FindFirst(JwtClaimTypes.TenantId)?.Value;
            return Guid.TryParse(value, out var tenantId) ? tenantId : null;
        }
    }
}
