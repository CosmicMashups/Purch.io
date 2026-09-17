namespace Purch.Application.Auth;

/// <summary>Who a redeemed refresh token belonged to — exactly one of UserId/DeviceId
/// pairing mirrors which IJwtTokenService.Issue* method originally applied: both set
/// for staff, DeviceId-only for kiosk, UserId-only for admin.</summary>
public sealed record RefreshTokenOwner(Guid TenantId, Guid? UserId, Guid? DeviceId);

public interface IRefreshTokenService
{
    /// <summary>Issues and persists (hashed) a new refresh token for the given owner.
    /// Returns the raw token to hand to the client — the only time it's ever available
    /// in plaintext, since only its hash is stored.</summary>
    Task<string> IssueAsync(Guid tenantId, Guid? userId, Guid? deviceId, CancellationToken cancellationToken = default);

    /// <summary>Validates a raw refresh token and atomically revokes it (single-use —
    /// each refresh rotates to a new token), returning who it belonged to. Returns null
    /// for a token that's missing, expired, or already revoked/redeemed, including a
    /// reused one — reuse of an already-rotated token is exactly the signal that the
    /// token leaked, so it's treated the same as any other invalid token rather than
    /// given special handling.</summary>
    Task<RefreshTokenOwner?> RedeemAsync(string rawToken, CancellationToken cancellationToken = default);

    /// <summary>Revokes a refresh token without issuing a replacement — used on logout
    /// so a stolen-but-not-yet-used token can't outlive the user's own session.</summary>
    Task RevokeAsync(string rawToken, CancellationToken cancellationToken = default);

    /// <summary>Revokes every outstanding refresh token for this user — used on password
    /// change so a session issued before the change can't keep renewing itself.</summary>
    Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Revokes every outstanding refresh token for this device — used when a
    /// device's pairing code or pairing PIN is reset, so a session from before the reset
    /// can't keep renewing itself.</summary>
    Task RevokeAllForDeviceAsync(Guid deviceId, CancellationToken cancellationToken = default);
}
