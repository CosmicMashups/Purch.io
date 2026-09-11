using Purch.Domain.Entities;

namespace Purch.Application.Catalog;

public interface IItemComboComponentRepository
{
    Task<IReadOnlyList<ItemComboComponent>> ListByItemAsync(Guid parentItemId, CancellationToken cancellationToken = default);

    void Add(ItemComboComponent component);
}
