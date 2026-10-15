using Purch.Domain.Entities;

namespace Purch.Application.Inventory;

public interface IStockTakeRepository
{
    /// <summary>Tracked, with its lines loaded.</summary>
    Task<StockTake?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Read-only (not tracked), with its lines loaded: used for a look before a transaction so that the
    /// tracked load inside the transaction is fresh from the database.</summary>
    Task<StockTake?> GetUntrackedAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Every take of the tenant, newest first, with line counts projected in the query.</summary>
    Task<IReadOnlyList<StockTakeSummaryDto>> ListSummariesAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Ids of the item/ingredient lines held by other Draft/Counting/Review takes of the tenant.</summary>
    Task<IReadOnlyList<(Guid? ItemId, Guid? InventoryItemId)>> ListOpenLineTargetsAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Movements of the given items/ingredients created after <paramref name="since"/> (read-only).</summary>
    Task<IReadOnlyList<InventoryMovement>> ListMovementsSinceAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> itemIds,
        IReadOnlyCollection<Guid> inventoryItemIds,
        DateTimeOffset since,
        CancellationToken cancellationToken = default);

    void Add(StockTake stockTake);
}
