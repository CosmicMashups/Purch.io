using Purch.Domain.Entities;

namespace Purch.Application.Catalog;

public interface IItemVariantRepository
{
    Task<ItemVariant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ItemVariant>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ItemVariant>> ListByItemAsync(Guid itemId, CancellationToken cancellationToken = default);

    void Add(ItemVariant variant);
}
