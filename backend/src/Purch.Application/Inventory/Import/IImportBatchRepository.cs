using Purch.Domain.Entities;

namespace Purch.Application.Inventory.Import;

public interface IImportBatchRepository
{
    /// <summary>Tracked.</summary>
    Task<ImportBatch?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Read-only (not tracked): a look before a transaction so the tracked load inside it is fresh.</summary>
    Task<ImportBatch?> GetUntrackedAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Read-only look-up of the tenant's batch uploaded with this Idempotency-Key.</summary>
    Task<ImportBatch?> FindByIdempotencyKeyAsync(Guid tenantId, string idempotencyKey, CancellationToken cancellationToken = default);

    void Add(ImportBatch batch);
}
