using Microsoft.EntityFrameworkCore;
using Purch.Application.Catalog;
using Purch.Domain.Entities;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfItemComboComponentRepository(PurchDbContext dbContext) : IItemComboComponentRepository
{
    public async Task<IReadOnlyList<ItemComboComponent>> ListByItemAsync(Guid parentItemId, CancellationToken cancellationToken = default)
    {
        return await dbContext.ItemComboComponents
            .AsNoTracking()
            .Where(component => component.ParentItemId == parentItemId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ItemComboComponent>> ListFixedByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.ItemComboComponents
            .AsNoTracking()
            .Where(component => component.TenantId == tenantId && component.ComponentItemId != null)
            .ToListAsync(cancellationToken);
    }

    public async Task<ItemComboComponent?> GetTrackedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.ItemComboComponents.FirstOrDefaultAsync(component => component.Id == id, cancellationToken);
    }

    public void Add(ItemComboComponent component)
    {
        _ = dbContext.ItemComboComponents.Add(component);
    }

    public void Remove(ItemComboComponent component)
    {
        _ = dbContext.ItemComboComponents.Remove(component);
    }
}
