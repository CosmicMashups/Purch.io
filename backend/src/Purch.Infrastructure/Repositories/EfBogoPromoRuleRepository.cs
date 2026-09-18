using Microsoft.EntityFrameworkCore;
using Purch.Application.Promotions;
using Purch.Domain.Entities;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfBogoPromoRuleRepository(PurchDbContext dbContext) : IBogoPromoRuleRepository
{
    public async Task<IReadOnlyList<BogoPromoRule>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.BogoPromoRules
            .AsNoTracking()
            .Where(rule => rule.TenantId == tenantId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BogoPromoRule>> ListActiveByTenantAsync(Guid tenantId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        return await dbContext.BogoPromoRules
            .AsNoTracking()
            .Where(rule => rule.TenantId == tenantId
                && rule.IsActive
                && (rule.StartsAt == null || rule.StartsAt <= now)
                && (rule.EndsAt == null || rule.EndsAt >= now))
            .ToListAsync(cancellationToken);
    }

    public Task<BogoPromoRule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.BogoPromoRules.FirstOrDefaultAsync(rule => rule.Id == id, cancellationToken);
    }

    public void Add(BogoPromoRule rule)
    {
        _ = dbContext.BogoPromoRules.Add(rule);
    }
}
