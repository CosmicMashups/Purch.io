using System.IdentityModel.Tokens.Jwt;
using Purch.Application.Auth;
using Purch.Application.Common;
using Purch.Domain.Enums;

namespace Purch.Api.Middleware;

/// <summary>Reads the staff/device/branch claims set on HttpContext.User by JWT auth — scoped per request.</summary>
public sealed class HttpContextCurrentActorProvider(IHttpContextAccessor httpContextAccessor) : ICurrentActorProvider
{
    public Guid? UserId => ParseGuidClaim(JwtRegisteredClaimNames.Sub);

    public Guid? DeviceId => ParseGuidClaim(JwtClaimTypes.DeviceId);

    public Guid? BranchId => ParseGuidClaim(JwtClaimTypes.BranchId);

    public ScopeType? ScopeType
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User.FindFirst(JwtClaimTypes.ScopeType)?.Value;
            return Enum.TryParse<ScopeType>(value, out var parsed) ? parsed : null;
        }
    }

    public Guid? ScopeId => ParseGuidClaim(JwtClaimTypes.ScopeId);

    private Guid? ParseGuidClaim(string claimType)
    {
        var value = httpContextAccessor.HttpContext?.User.FindFirst(claimType)?.Value;
        return Guid.TryParse(value, out var parsed) ? parsed : null;
    }
}
