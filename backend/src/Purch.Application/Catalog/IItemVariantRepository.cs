using Purch.Domain.Entities;

namespace Purch.Application.Catalog;

public interface IItemVariantRepository
{
    Task<IReadOnlyList<ItemVariant>> ListByItemAsync(Guid itemId, CancellationToken cancellationToken = default);

    void Add(ItemVariant variant);
}
