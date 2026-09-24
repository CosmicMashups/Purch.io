using Microsoft.EntityFrameworkCore;
using Purch.Application.Catalog;
using Purch.Domain.Entities;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfItemRepository(PurchDbContext dbContext) : IItemRepository
{
    public Task<Item?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.Items.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Item>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        return ids.Count == 0
            ? []
            : await dbContext.Items.AsNoTracking().Where(item => ids.Contains(item.Id)).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Item>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Items
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> BarcodeExistsAsync(Guid tenantId, string barcode, CancellationToken cancellationToken = default)
    {
        return dbContext.Items
            .AsNoTracking()
            .AnyAsync(item => item.TenantId == tenantId && item.Barcode == barcode, cancellationToken);
    }

    public void Add(Item item)
    {
        _ = dbContext.Items.Add(item);
    }
}
