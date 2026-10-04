using Microsoft.EntityFrameworkCore;
using Purch.Application.Inventory;
using Purch.Domain.Entities;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfInventoryCategoryRepository(PurchDbContext dbContext) : IInventoryCategoryRepository
{
    public Task<InventoryCategory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.InventoryCategories.FirstOrDefaultAsync(category => category.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<InventoryCategory>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.InventoryCategories
            .AsNoTracking()
            .Where(category => category.TenantId == tenantId)
            .ToListAsync(cancellationToken);
    }

    public void Add(InventoryCategory category)
    {
        _ = dbContext.InventoryCategories.Add(category);
    }

    public void Remove(InventoryCategory category)
    {
        _ = dbContext.InventoryCategories.Remove(category);
    }
}
