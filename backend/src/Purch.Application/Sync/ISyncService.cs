namespace Purch.Application.Sync;

public interface ISyncService
{
    /// <summary>Processes a batch in one round trip — see docs/ARCHITECTURE.md §3
    /// ("don't make the client sync item-by-item").</summary>
    Task<SyncBatchResultDto> SyncBatchAsync(SyncBatchRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FlaggedSyncRecordDto>> ListFlaggedAsync(CancellationToken cancellationToken = default);

    Task<FlaggedSyncRecordDto> AcknowledgeFlaggedAsync(Guid syncedRecordId, CancellationToken cancellationToken = default);
}
