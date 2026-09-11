using Purch.Domain.Entities;

namespace Purch.Application.Catalog;

public interface IItemModifierGroupRepository
{
    Task<IReadOnlyList<Guid>> ListGroupIdsForItemAsync(Guid itemId, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid itemId, Guid modifierGroupId, CancellationToken cancellationToken = default);

    void Add(ItemModifierGroup link);
}
