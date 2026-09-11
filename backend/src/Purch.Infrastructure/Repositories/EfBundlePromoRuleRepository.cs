using Microsoft.EntityFrameworkCore;
using Purch.Application.Catalog;
using Purch.Domain.Entities;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfBundlePromoRuleRepository(PurchDbContext dbContext) : IBundlePromoRuleRepository
{
    public async Task<IReadOnlyList<BundlePromoRule>> ListByItemAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        return await dbContext.BundlePromoRules
            .AsNoTracking()
            .Where(rule => rule.ItemId == itemId)
            .ToListAsync(cancellationToken);
    }

    public void Add(BundlePromoRule rule)
    {
        _ = dbContext.BundlePromoRules.Add(rule);
    }
}
