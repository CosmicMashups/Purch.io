using System.IdentityModel.Tokens.Jwt;
using Purch.Application.Auth;
using Purch.Application.Common;

namespace Purch.Api.Middleware;

/// <summary>Reads the staff/device/branch claims set on HttpContext.User by JWT auth — scoped per request.</summary>
public sealed class HttpContextCurrentActorProvider(IHttpContextAccessor httpContextAccessor) : ICurrentActorProvider
{
    public Guid? UserId => ParseClaim(JwtRegisteredClaimNames.Sub);

    public Guid? DeviceId => ParseClaim(JwtClaimTypes.DeviceId);

    public Guid? BranchId => ParseClaim(JwtClaimTypes.BranchId);

    private Guid? ParseClaim(string claimType)
    {
        var value = httpContextAccessor.HttpContext?.User.FindFirst(claimType)?.Value;
        return Guid.TryParse(value, out var parsed) ? parsed : null;
    }
}
