using Purch.Domain.Entities;

namespace Purch.Application.Catalog;

public interface IItemComboComponentRepository
{
    Task<IReadOnlyList<ItemComboComponent>> ListByItemAsync(Guid parentItemId, CancellationToken cancellationToken = default);

    /// <summary>Every fixed slot (one that names a specific item) in the tenant, for the catalog's availability check.</summary>
    Task<IReadOnlyList<ItemComboComponent>> ListFixedByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>A tracked slot, ready to be edited or removed.</summary>
    Task<ItemComboComponent?> GetTrackedAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(ItemComboComponent component);

    void Remove(ItemComboComponent component);
}
