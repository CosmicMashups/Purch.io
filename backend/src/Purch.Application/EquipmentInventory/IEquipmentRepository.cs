using Purch.Domain.Entities;

namespace Purch.Application.EquipmentInventory;

public interface IEquipmentRepository
{
    /// <summary>Not deleted; includes inactive. Untracked.</summary>
    Task<IReadOnlyList<Equipment>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<Equipment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Equipment>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    void Add(Equipment equipment);
}

public interface IItemEquipmentRepository
{
    /// <summary>Tracked, so a replace can remove them in the same save.</summary>
    Task<IReadOnlyList<ItemEquipment>> ListByItemAsync(Guid itemId, CancellationToken cancellationToken = default);

    /// <summary>Every link for the tenant, so a full catalog listing can work out availability in one query.</summary>
    Task<IReadOnlyList<ItemEquipment>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ItemEquipment>> ListByItemsAsync(IReadOnlyCollection<Guid> itemIds, CancellationToken cancellationToken = default);

    /// <summary>How many non-deleted items need this equipment — for the delete/deactivate impact note and the list.</summary>
    Task<IReadOnlyDictionary<Guid, int>> CountItemsByEquipmentAsync(Guid tenantId, CancellationToken cancellationToken = default);

    void AddRange(IEnumerable<ItemEquipment> links);

    void RemoveRange(IEnumerable<ItemEquipment> links);
}
