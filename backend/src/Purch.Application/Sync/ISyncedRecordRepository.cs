using Purch.Domain.Entities;

namespace Purch.Application.Sync;

public interface ISyncedRecordRepository
{
    Task<SyncedRecord?> GetByIdempotencyKeyAsync(Guid tenantId, string idempotencyKey, CancellationToken cancellationToken = default);

    Task<SyncedRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The current, un-flagged winner for this entity (there is at
    /// most one at any time — see SyncService), or null if none has synced yet.</summary>
    Task<SyncedRecord?> GetCurrentWinnerAsync(Guid tenantId, string entityType, Guid entityId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SyncedRecord>> ListFlaggedAsync(Guid tenantId, CancellationToken cancellationToken = default);

    void Add(SyncedRecord record);
}
