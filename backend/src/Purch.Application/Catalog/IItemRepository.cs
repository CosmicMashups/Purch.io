using Purch.Domain.Entities;

namespace Purch.Application.Catalog;

public interface IItemRepository
{
    Task<Item?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Read-only batch lookup — one query for many ids.</summary>
    Task<IReadOnlyList<Item>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Item>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<bool> BarcodeExistsAsync(Guid tenantId, string barcode, CancellationToken cancellationToken = default);

    void Add(Item item);
}
