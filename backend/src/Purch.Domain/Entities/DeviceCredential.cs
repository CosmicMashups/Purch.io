using Purch.Domain.Common;

namespace Purch.Domain.Entities;

/// <summary>What a paired device holds after its one-time pairing code is entered: a long-lived secret that identifies
/// the device without any person signing in. Only the hash is stored. Revoking the device revokes this.</summary>
public class DeviceCredential : TenantScopedEntity
{
    public Guid DeviceId { get; set; }

    /// <summary>SHA-256 of the secret. Looked up without a tenant, like a refresh token.</summary>
    public string CredentialHash { get; set; } = string.Empty;

    public DateTimeOffset? LastUsedAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }
}
