using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Purch.Application.Auth;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Infrastructure.Auth;

public sealed class JwtTokenService(IConfiguration configuration) : IJwtTokenService
{
    private readonly IConfiguration _configuration = configuration;

    public string IssueAccessToken(User user, Device device)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtClaimTypes.TenantId, user.TenantId.ToString()),
            new(JwtClaimTypes.Role, user.Role.ToString()),
            new(JwtClaimTypes.ScopeType, user.ScopeType.ToString()),
            new(JwtClaimTypes.DeviceId, device.Id.ToString()),
            // The device's own branch, not the user's — a tenant-scoped staff
            // member's User.BranchId is null, but a transaction always happens
            // at one physical terminal's branch.
            new(JwtClaimTypes.BranchId, device.BranchId.ToString()),
        };

        if (user.ScopeId is { } scopeId)
        {
            claims.Add(new Claim(JwtClaimTypes.ScopeId, scopeId.ToString()));
        }

        return WriteToken(claims, TimeSpan.FromHours(8));
    }

    public string IssueAdminAccessToken(User user)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtClaimTypes.TenantId, user.TenantId.ToString()),
            new(JwtClaimTypes.Role, user.Role.ToString()),
            new(JwtClaimTypes.ScopeType, user.ScopeType.ToString()),
        };

        if (user.ScopeId is { } scopeId)
        {
            claims.Add(new Claim(JwtClaimTypes.ScopeId, scopeId.ToString()));
        }

        return WriteToken(claims, TimeSpan.FromHours(8));
    }

    public string IssueKioskAccessToken(Device device)
    {
        var claims = new List<Claim>
        {
            new(JwtClaimTypes.TenantId, device.TenantId.ToString()),
            new(JwtClaimTypes.Role, Role.Kiosk.ToString()),
            new(JwtClaimTypes.DeviceId, device.Id.ToString()),
            new(JwtClaimTypes.BranchId, device.BranchId.ToString()),
        };

        // A stationary, unattended terminal — no one re-pairs it every shift the
        // way a staff member re-logs-in, so this carries a longer expiry than a
        // staff session token.
        return WriteToken(claims, TimeSpan.FromHours(24));
    }

    private string WriteToken(IReadOnlyList<Claim> claims, TimeSpan validFor)
    {
        var signingKey = _configuration["JWT_SIGNING_KEY"]
            ?? throw new InvalidOperationException("JWT_SIGNING_KEY is not configured.");
        var issuer = _configuration["JWT_ISSUER"] ?? "purch.io";

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: issuer,
            claims: claims,
            expires: DateTime.UtcNow.Add(validFor),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
