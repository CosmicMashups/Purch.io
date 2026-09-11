using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Purch.Application.Auth;
using Purch.Domain.Entities;

namespace Purch.Infrastructure.Auth;

public sealed class JwtTokenService(IConfiguration configuration) : IJwtTokenService
{
    private readonly IConfiguration _configuration = configuration;

    public string IssueAccessToken(User user, Device device)
    {
        var signingKey = _configuration["JWT_SIGNING_KEY"]
            ?? throw new InvalidOperationException("JWT_SIGNING_KEY is not configured.");
        var issuer = _configuration["JWT_ISSUER"] ?? "purch.io";

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

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: issuer,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
