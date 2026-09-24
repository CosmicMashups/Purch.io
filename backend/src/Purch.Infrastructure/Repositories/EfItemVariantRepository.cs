using Microsoft.EntityFrameworkCore;
using Purch.Application.Catalog;
using Purch.Domain.Entities;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfItemVariantRepository(PurchDbContext dbContext) : IItemVariantRepository
{
    public Task<ItemVariant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.ItemVariants.FirstOrDefaultAsync(variant => variant.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<ItemVariant>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        return ids.Count == 0
            ? []
            : await dbContext.ItemVariants.AsNoTracking().Where(variant => ids.Contains(variant.Id)).ToListAsync(cancellationToken);
    }

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
