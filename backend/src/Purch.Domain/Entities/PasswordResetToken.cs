using Purch.Domain.Common;

namespace Purch.Domain.Entities;

/// <summary>A short-lived, single-use credential used to authorize an admin password
/// change without re-entering the old password — issued by /auth/password-reset/request,
/// redeemed by /auth/password-reset/confirm. Never stored in plaintext — only TokenHash
/// is persisted, same pattern as <see cref="RefreshToken"/>.</summary>
public class PasswordResetToken : TenantScopedEntity
{
    public Guid UserId { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Set once this token has been redeemed — a token can never be redeemed twice.</summary>
    public DateTimeOffset? UsedAt { get; set; }
}
