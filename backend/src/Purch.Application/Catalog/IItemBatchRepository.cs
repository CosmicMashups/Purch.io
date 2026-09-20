using Purch.Domain.Entities;

namespace Purch.Application.Catalog;

public interface IItemBatchRepository
{
    /// <summary>Oldest-first — the natural order for FIFO stock-out once that logic lands.</summary>
    Task<IReadOnlyList<ItemBatch>> ListByItemAsync(Guid itemId, CancellationToken cancellationToken = default);

    /// <summary>Tracked batches that still have stock, in the order a sale should use them: earliest
    /// expiry first (batches with no expiry last), then oldest received.</summary>
    Task<IReadOnlyList<ItemBatch>> ListConsumableAsync(Guid itemId, CancellationToken cancellationToken = default);

    void Add(ItemBatch batch);
}
