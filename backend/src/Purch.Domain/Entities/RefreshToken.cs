using Purch.Domain.Common;

namespace Purch.Domain.Entities;

/// <summary>A long-lived, single-use credential exchanged for a new access token
/// once the short-lived one expires, so a staff/admin/kiosk session survives past
/// the access token's lifetime without forcing a manual re-login. Never stored in
/// plaintext — only TokenHash is persisted; the raw value is handed to the client
/// once, at issue time, and never seen again server-side.</summary>
public class RefreshToken : TenantScopedEntity
{
    /// <summary>Set for staff and admin logins; null for a kiosk pairing (no user).</summary>
    public Guid? UserId { get; set; }

    /// <summary>Set for staff and kiosk sessions; null for an admin back-office login
    /// (not tied to any physical terminal).</summary>
    public Guid? DeviceId { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Set once this token has been redeemed (rotated) or explicitly revoked
    /// via logout — either way, it can never be redeemed again.</summary>
    public DateTimeOffset? RevokedAt { get; set; }
}
