using Purch.Domain.Entities;

namespace Purch.Application.Promotions;

public interface IPromoCodeRepository
{
    Task<IReadOnlyList<PromoCode>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Case-insensitive match, since cashiers/customers won't reliably match the stored casing.</summary>
    Task<PromoCode?> GetByCodeAsync(Guid tenantId, string code, CancellationToken cancellationToken = default);

    void Add(PromoCode promoCode);
}
