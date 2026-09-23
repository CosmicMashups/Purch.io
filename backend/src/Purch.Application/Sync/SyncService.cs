using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Domain.Entities;

namespace Purch.Application.Sync;

/// <summary>Phase 6 — the server side of the offline sync engine (see
/// docs/adr/0003, docs/ARCHITECTURE.md §5). Two guarantees, both built on
/// the SyncedRecord ledger:
/// 1. Idempotency: replaying an already-recorded IdempotencyKey (e.g. after
///    a client retry following a dropped response) is a safe no-op.
/// 2. Conflict resolution: when two different devices submit differing
///    state for the same entity, the later-ClientTimestamp one is flagged
///    for manual review — never applied silently, never dropped. Processed
///    one item at a time with a SaveChanges after each, so a later item in
///    the same batch always sees the prior item's committed result when it
///    looks up the "current winner" for its entity.
/// This deliberately stops at ledger-keeping and conflict flagging, not at
/// actually mutating arbitrary entities from PayloadJson — see the doc
/// comment on ISyncService for why that's a separate, far larger problem
/// (a full per-entity-type command dispatcher) that nothing in the spec
/// concretely defines yet.</summary>
public sealed class SyncService(
    ISyncedRecordRepository syncedRecordRepository,
    ICurrentTenantProvider currentTenantProvider,
    ICurrentActorProvider currentActorProvider,
    IUnitOfWork unitOfWork) : ISyncService
{
    public async Task<SyncBatchResultDto> SyncBatchAsync(SyncBatchRequest request, CancellationToken cancellationToken = default)
    {
        var results = new List<SyncItemResultDto>();

        foreach (var item in request.Items)
        {
            results.Add(await SyncOneAsync(item, cancellationToken));
        }

        return new SyncBatchResultDto(results);
    }

    private async Task<SyncItemResultDto> SyncOneAsync(SyncItemRequest item, CancellationToken cancellationToken)
    {
        var existing = await syncedRecordRepository.GetByIdempotencyKeyAsync(CurrentTenantId, item.IdempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return new SyncItemResultDto(item.IdempotencyKey, SyncItemStatus.AlreadySynced, "Already recorded — no change made.");
        }

        var currentWinner = await syncedRecordRepository.GetCurrentWinnerAsync(CurrentTenantId, item.EntityType, item.EntityId, cancellationToken);

        var newRecord = new SyncedRecord
        {
            TenantId = CurrentTenantId,
            DeviceId = CurrentDeviceId,
            IdempotencyKey = item.IdempotencyKey,
            EntityType = item.EntityType,
            EntityId = item.EntityId,
            ClientTimestamp = item.ClientTimestamp,
            FlaggedForReview = false,
        };

        var isCrossDeviceConflict = currentWinner is not null
            && currentWinner.DeviceId != CurrentDeviceId
            && currentWinner.ClientTimestamp != item.ClientTimestamp;

        if (!isCrossDeviceConflict)
        {
            syncedRecordRepository.Add(newRecord);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            return new SyncItemResultDto(item.IdempotencyKey, SyncItemStatus.Applied, "Recorded.");
        }

        if (item.ClientTimestamp > currentWinner!.ClientTimestamp)
        {
            // This item is the later one — it loses, the existing winner stands.
            newRecord.FlaggedForReview = true;
            syncedRecordRepository.Add(newRecord);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            return new SyncItemResultDto(
                item.IdempotencyKey,
                SyncItemStatus.ConflictFlagged,
                $"Another device already holds a newer change to this {item.EntityType} — flagged for manual review.");
        }

        // This item is actually earlier than what we thought was the winner —
        // it wins instead, and the previous winner is retroactively flagged.
        currentWinner.FlaggedForReview = true;
        syncedRecordRepository.Add(newRecord);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        return new SyncItemResultDto(item.IdempotencyKey, SyncItemStatus.Applied, "Recorded — superseded a later-timestamped conflicting change, which was flagged for review.");
    }

    public async Task<IReadOnlyList<FlaggedSyncRecordDto>> ListFlaggedAsync(int? limit = null, CancellationToken cancellationToken = default)
    {
        var flagged = await syncedRecordRepository.ListFlaggedAsync(CurrentTenantId, limit, cancellationToken);
        return [.. flagged.Select(ToDto)];
    }

    public async Task<FlaggedSyncRecordDto> AcknowledgeFlaggedAsync(Guid syncedRecordId, CancellationToken cancellationToken = default)
    {
        var record = await syncedRecordRepository.GetByIdAsync(syncedRecordId, cancellationToken);
        if (record is null || record.TenantId != CurrentTenantId || !record.FlaggedForReview)
        {
            throw new NotFoundException("Flagged sync record", syncedRecordId);
        }

        record.ReviewedAt = DateTimeOffset.UtcNow;
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(record);
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("Sync requires an authenticated tenant context.");

    private Guid CurrentDeviceId => currentActorProvider.DeviceId
        ?? throw new InvalidOperationException("Sync requires an authenticated device context.");

    private static FlaggedSyncRecordDto ToDto(SyncedRecord record)
    {
        return new(record.Id, record.DeviceId, record.EntityType, record.EntityId, record.ClientTimestamp, record.ReviewedAt);
    }
}
