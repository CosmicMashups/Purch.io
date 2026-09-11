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

    public bool FlaggedForReview { get; set; }
}
