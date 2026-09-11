using Purch.Domain.Entities;

namespace Purch.Application.Inventory;

public interface ISupplierRepository
{
    Task<IReadOnlyList<Supplier>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(Supplier supplier);
}
