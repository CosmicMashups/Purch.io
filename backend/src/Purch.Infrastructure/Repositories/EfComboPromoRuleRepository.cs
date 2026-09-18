using Microsoft.EntityFrameworkCore;
using Purch.Application.Promotions;
using Purch.Domain.Entities;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfComboPromoRuleRepository(PurchDbContext dbContext) : IComboPromoRuleRepository
{
    public async Task<IReadOnlyList<ComboPromoRule>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.ComboPromoRules
            .AsNoTracking()
            .Where(rule => rule.TenantId == tenantId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ComboPromoRule>> ListActiveByTenantAsync(Guid tenantId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        return await dbContext.ComboPromoRules
            .AsNoTracking()
            .Where(rule => rule.TenantId == tenantId
                && rule.IsActive
                && (rule.StartsAt == null || rule.StartsAt <= now)
                && (rule.EndsAt == null || rule.EndsAt >= now))
            .ToListAsync(cancellationToken);
    }

    public Task<ComboPromoRule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.ComboPromoRules.FirstOrDefaultAsync(rule => rule.Id == id, cancellationToken);
    }

    public void Add(ComboPromoRule rule)
    {
        _ = dbContext.ComboPromoRules.Add(rule);
    }
}
