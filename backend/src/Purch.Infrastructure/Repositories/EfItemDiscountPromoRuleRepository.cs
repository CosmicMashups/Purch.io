using Microsoft.EntityFrameworkCore;
using Purch.Application.Promotions;
using Purch.Domain.Entities;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfItemDiscountPromoRuleRepository(PurchDbContext dbContext) : IItemDiscountPromoRuleRepository
{
    public async Task<IReadOnlyList<ItemDiscountPromoRule>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.ItemDiscountPromoRules
            .AsNoTracking()
            .Where(rule => rule.TenantId == tenantId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ItemDiscountPromoRule>> ListActiveByTenantAsync(Guid tenantId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        return await dbContext.ItemDiscountPromoRules
            .AsNoTracking()
            .Where(rule => rule.TenantId == tenantId
                && rule.IsActive
                && (rule.StartsAt == null || rule.StartsAt <= now)
                && (rule.EndsAt == null || rule.EndsAt >= now))
            .ToListAsync(cancellationToken);
    }

    public Task<ItemDiscountPromoRule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.ItemDiscountPromoRules.FirstOrDefaultAsync(rule => rule.Id == id, cancellationToken);
    }

    public void Add(ItemDiscountPromoRule rule)
    {
        _ = dbContext.ItemDiscountPromoRules.Add(rule);
    }
}
