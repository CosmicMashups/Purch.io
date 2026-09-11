using Purch.Domain.Entities;

namespace Purch.Application.Catalog;

public interface IBundlePromoRuleRepository
{
    Task<IReadOnlyList<BundlePromoRule>> ListByItemAsync(Guid itemId, CancellationToken cancellationToken = default);

    void Add(BundlePromoRule rule);
}
