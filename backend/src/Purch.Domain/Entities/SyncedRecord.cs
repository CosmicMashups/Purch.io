using Purch.Domain.Common;

namespace Purch.Domain.Entities;

/// <summary>
/// Server-side ledger of records received via /sync, keyed by client-generated
/// idempotency key. Full conflict-resolution logic (later-timestamp-auto-cancel,
/// manual-review flagging) lands in Phase 6 — see docs/adr/0003.
/// </summary>
public class SyncedRecord : TenantScopedEntity
{
    public Guid DeviceId { get; set; }

    public string IdempotencyKey { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public Guid EntityId { get; set; }

    /// <summary>When the client made this change — the basis for the
    /// later-timestamp-auto-cancel conflict rule, not when the server
    /// received it (that's CreatedAt, inherited from Entity).</summary>
    public DateTimeOffset ClientTimestamp { get; set; }

    /// <summary>True once a cross-device conflict on the same entity marked
    /// this record as the losing (later-timestamped) side. A flagged record
    /// is permanently excluded from "current winner" lookups — see
    /// SyncService — even after a human acknowledges it via ReviewedAt.</summary>
    public bool FlaggedForReview { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }
}
