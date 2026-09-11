using Microsoft.EntityFrameworkCore;
using Purch.Application.Catalog;
using Purch.Domain.Entities;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfItemModifierGroupRepository(PurchDbContext dbContext) : IItemModifierGroupRepository
{
    public async Task<IReadOnlyList<Guid>> ListGroupIdsForItemAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        return await dbContext.ItemModifierGroups
            .AsNoTracking()
            .Where(link => link.ItemId == itemId)
            .Select(link => link.ModifierGroupId)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsAsync(Guid itemId, Guid modifierGroupId, CancellationToken cancellationToken = default)
    {
        return dbContext.ItemModifierGroups
            .AsNoTracking()
            .AnyAsync(link => link.ItemId == itemId && link.ModifierGroupId == modifierGroupId, cancellationToken);
    }

    public void Add(ItemModifierGroup link)
    {
        _ = dbContext.ItemModifierGroups.Add(link);
    }
}
