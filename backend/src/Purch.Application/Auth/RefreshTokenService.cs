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

    /// <summary>A just-rotated token stays redeemable for this long. The rotation commits before the
    /// response reaches the client, so a dropped connection or a killed app between the two would
    /// otherwise leave the client holding a token the server already burned — and force a full
    /// re-login over what was only a network hiccup. The retry simply gets another fresh token.</summary>
    private static readonly TimeSpan RotationGrace = TimeSpan.FromSeconds(60);

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

        var now = DateTimeOffset.UtcNow;
        var existing = await refreshTokenRepository.FindByTokenHashAsync(Hash(rawToken), cancellationToken);
        if (existing is null || existing.ExpiresAt <= now)
        {
            return null;
        }

        // Rotated (RevokedAt set) tokens are only honoured inside the grace window. A token revoked
        // by logout/password change/device reset also has its ExpiresAt pulled to that moment (see
        // Expire below), so it fails the check above and can never come back through the grace path.
        if (existing.RevokedAt is { } revokedAt && now - revokedAt > RotationGrace)
        {
            return null;
        }

        // Only STAGES the revocation — deliberately not saved here. The caller issues the replacement
        // next, and IssueAsync's SaveChanges commits the revoke and the new token together, so a
        // failure between the two can never burn the client's only token without giving it a new one.
        existing.RevokedAt ??= now;

        return new RefreshTokenOwner(existing.TenantId, existing.UserId, existing.DeviceId);
    }

    public async Task RevokeAsync(string rawToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return;
        }

        var existing = await refreshTokenRepository.FindByTokenHashAsync(Hash(rawToken), cancellationToken);
        if (existing is null)
        {
            return;
        }

        Expire(existing, DateTimeOffset.UtcNow);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Ends a token for good: revoked AND expired now, so neither the normal path nor the
    /// rotation grace window will ever accept it again.</summary>
    private static void Expire(RefreshToken token, DateTimeOffset at)
    {
        token.RevokedAt = at;
        token.ExpiresAt = at;
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
            Expire(token, revokedAt);
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
            Expire(token, revokedAt);
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
