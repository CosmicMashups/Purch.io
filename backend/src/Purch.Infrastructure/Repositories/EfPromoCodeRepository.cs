using Microsoft.EntityFrameworkCore;
using Purch.Application.Promotions;
using Purch.Domain.Entities;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfPromoCodeRepository(PurchDbContext dbContext) : IPromoCodeRepository
{
    public async Task<IReadOnlyList<PromoCode>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.PromoCodes
            .AsNoTracking()
            .Where(promoCode => promoCode.TenantId == tenantId)
            .ToListAsync(cancellationToken);
    }

    public Task<PromoCode?> GetByCodeAsync(Guid tenantId, string code, CancellationToken cancellationToken = default)
    {
        var pattern = code.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
        return dbContext.PromoCodes
            .FirstOrDefaultAsync(promoCode => promoCode.TenantId == tenantId && EF.Functions.ILike(promoCode.Code, pattern, "\\"), cancellationToken);
    }

    public void Add(PromoCode promoCode)
    {
        _ = dbContext.PromoCodes.Add(promoCode);
    }
}
