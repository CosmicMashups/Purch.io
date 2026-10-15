using Purch.Domain.Entities;

namespace Purch.Application.Inventory;

public interface IStockTakeRepository
{
    /// <summary>Tracked, with its lines loaded.</summary>
    Task<StockTake?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Every take of the tenant with its lines, newest first (read-only).</summary>
    Task<IReadOnlyList<StockTake>> ListAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Ids of the item/ingredient lines held by other Draft/Counting/Review takes of the tenant.</summary>
    Task<IReadOnlyList<(Guid? ItemId, Guid? InventoryItemId)>> ListOpenLineTargetsAsync(Guid tenantId, CancellationToken cancellationToken = default);

    void Add(StockTake stockTake);
}
