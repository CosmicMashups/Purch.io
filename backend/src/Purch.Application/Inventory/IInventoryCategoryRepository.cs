using Purch.Domain.Entities;

namespace Purch.Application.Inventory;

public interface IInventoryCategoryRepository
{
    Task<InventoryCategory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InventoryCategory>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    void Add(InventoryCategory category);

    void Remove(InventoryCategory category);
}

public interface IInventoryCategoryService
{
    Task<IReadOnlyList<InventoryCategoryDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<InventoryCategoryDto> CreateAsync(CreateInventoryCategoryRequest request, CancellationToken cancellationToken = default);

    Task<InventoryCategoryDto> UpdateAsync(Guid id, UpdateInventoryCategoryRequest request, CancellationToken cancellationToken = default);

    /// <summary>Removes the category; its ingredients become uncategorised rather than being deleted.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
