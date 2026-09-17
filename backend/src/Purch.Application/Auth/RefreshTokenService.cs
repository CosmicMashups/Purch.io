using System.Security.Cryptography;
using System.Text;
using Purch.Application.Common;
using Purch.Domain.Entities;

namespace Purch.Application.Auth;

public sealed class RefreshTokenService(
    IRefreshTokenRepository refreshTokenRepository,
    IUnitOfWork unitOfWork) : IRefreshTokenService
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    public async Task<string> IssueAsync(Guid tenantId, Guid? userId, Guid? deviceId, CancellationToken cancellationToken = default)
    {
        var rawToken = GenerateRawToken();

        refreshTokenRepository.Add(new RefreshToken
        {
            TenantId = tenantId,
            UserId = userId,
            DeviceId = deviceId,
            TokenHash = Hash(rawToken),
            ExpiresAt = DateTimeOffset.UtcNow.Add(Lifetime),
        });

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        return rawToken;
    }

    public async Task<RefreshTokenOwner?> RedeemAsync(string rawToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return null;
        }

        var existing = await refreshTokenRepository.FindByTokenHashAsync(Hash(rawToken), cancellationToken);
        if (existing is null || existing.RevokedAt is not null || existing.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return null;
        }

        existing.RevokedAt = DateTimeOffset.UtcNow;
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return new RefreshTokenOwner(existing.TenantId, existing.UserId, existing.DeviceId);
    }

    public async Task RevokeAsync(string rawToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return;
        }

        var existing = await refreshTokenRepository.FindByTokenHashAsync(Hash(rawToken), cancellationToken);
        if (existing is null || existing.RevokedAt is not null)
        {
            return;
        }

        existing.RevokedAt = DateTimeOffset.UtcNow;
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var activeTokens = await refreshTokenRepository.ListActiveByUserIdAsync(userId, cancellationToken);
        if (activeTokens.Count == 0)
        {
            return;
        }

        var revokedAt = DateTimeOffset.UtcNow;
        foreach (var token in activeTokens)
        {
            token.RevokedAt = revokedAt;
        }

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeAllForDeviceAsync(Guid deviceId, CancellationToken cancellationToken = default)
    {
        var activeTokens = await refreshTokenRepository.ListActiveByDeviceIdAsync(deviceId, cancellationToken);
        if (activeTokens.Count == 0)
        {
            return;
        }

        var revokedAt = DateTimeOffset.UtcNow;
        foreach (var token in activeTokens)
        {
            token.RevokedAt = revokedAt;
        }

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static string GenerateRawToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static string Hash(string rawToken)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
    }
}
