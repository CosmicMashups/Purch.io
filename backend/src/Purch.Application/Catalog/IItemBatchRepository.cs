using Purch.Domain.Entities;

namespace Purch.Application.Catalog;

public interface IItemBatchRepository
{
    /// <summary>Oldest-first — the natural order for FIFO stock-out once that logic lands.</summary>
    Task<IReadOnlyList<ItemBatch>> ListByItemAsync(Guid itemId, CancellationToken cancellationToken = default);

    void Add(ItemBatch batch);
}
