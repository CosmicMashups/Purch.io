using Microsoft.EntityFrameworkCore;
using Purch.Application.Inventory;
using Purch.Domain.Entities;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfSupplierRepository(PurchDbContext dbContext) : ISupplierRepository
{
    public async Task<IReadOnlyList<Supplier>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Suppliers
            .AsNoTracking()
            .Where(supplier => supplier.TenantId == tenantId)
            .ToListAsync(cancellationToken);
    }

    public Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.Suppliers.FirstOrDefaultAsync(supplier => supplier.Id == id, cancellationToken);
    }

    public void Add(Supplier supplier)
    {
        _ = dbContext.Suppliers.Add(supplier);
    }
}
