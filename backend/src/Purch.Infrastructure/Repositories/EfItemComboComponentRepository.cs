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

    public void Add(ItemComboComponent component)
    {
        _ = dbContext.ItemComboComponents.Add(component);
    }
}
