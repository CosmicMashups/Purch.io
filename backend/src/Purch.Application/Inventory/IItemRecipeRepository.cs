using Purch.Domain.Entities;

namespace Purch.Application.Inventory;

public interface IItemRecipeRepository
{
    Task<IReadOnlyList<ItemRecipeLine>> ListByItemAsync(Guid itemId, CancellationToken cancellationToken = default);

    /// <summary>All recipe lines for the tenant, across every item — used to batch-compute
    /// availability for a full catalog listing instead of querying once per item.</summary>
    Task<IReadOnlyList<ItemRecipeLine>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    void AddRange(IEnumerable<ItemRecipeLine> lines);

    void RemoveRange(IEnumerable<ItemRecipeLine> lines);
}
