namespace Purch.Application.Sync;

/// <summary>One queued offline write, as the client's local
/// pending_sync_queue table stores it — see docs/ARCHITECTURE.md §5.</summary>
public sealed record SyncItemRequest(
    string IdempotencyKey,
    string EntityType,
    Guid EntityId,
    DateTimeOffset ClientTimestamp,
    string PayloadJson);

public sealed record SyncBatchRequest(IReadOnlyList<SyncItemRequest> Items);

public enum SyncItemStatus
{
    /// <summary>Recorded as the current winner for its entity.</summary>
    Applied,

    /// <summary>This exact IdempotencyKey was already recorded — safe to
    /// discard from the local queue, nothing new happened.</summary>
    AlreadySynced,

    /// <summary>A different device already holds (or now holds) the winning
    /// state for this entity and this item lost the later-timestamp
    /// comparison — flagged for manual review, not applied, not dropped.</summary>
    ConflictFlagged,
}

public sealed record SyncItemResultDto(string IdempotencyKey, SyncItemStatus Status, string Message);

public sealed record SyncBatchResultDto(IReadOnlyList<SyncItemResultDto> Results);

/// <summary>A record flagged by the conflict rule, for F-series manual review.</summary>
public sealed record FlaggedSyncRecordDto(
    Guid Id,
    Guid DeviceId,
    string EntityType,
    Guid EntityId,
    DateTimeOffset ClientTimestamp,
    DateTimeOffset? ReviewedAt);
