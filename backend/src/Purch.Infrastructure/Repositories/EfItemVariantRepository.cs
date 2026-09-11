using Microsoft.EntityFrameworkCore;
using Purch.Application.Catalog;
using Purch.Domain.Entities;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfItemVariantRepository(PurchDbContext dbContext) : IItemVariantRepository
{
    public async Task<IReadOnlyList<ItemVariant>> ListByItemAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        return await dbContext.ItemVariants
            .AsNoTracking()
            .Where(variant => variant.ItemId == itemId)
            .ToListAsync(cancellationToken);
    }

    public void Add(ItemVariant variant)
    {
        _ = dbContext.ItemVariants.Add(variant);
    }
}
