using Purch.Domain.Entities;

namespace Purch.Application.Inventory;

public interface IInventoryItemRepository
{
    Task<IReadOnlyList<InventoryItem>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<InventoryItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InventoryItem>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    Task<InventoryItem?> GetByLinkedItemIdAsync(Guid tenantId, Guid itemId, CancellationToken cancellationToken = default);

    /// <summary>Tracked, so a category being deleted can clear it from its ingredients in the same save.</summary>
    Task<IReadOnlyList<InventoryItem>> ListByCategoryTrackedAsync(Guid categoryId, CancellationToken cancellationToken = default);

    void Add(InventoryItem inventoryItem);
}
