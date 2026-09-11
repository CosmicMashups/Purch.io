namespace Purch.Application.Catalog;

public interface IBundlePromoRuleService
{
    Task<IReadOnlyList<BundlePromoRuleDto>> ListForItemAsync(Guid itemId, CancellationToken cancellationToken = default);

    Task<BundlePromoRuleDto> CreateAsync(
        Guid itemId,
        CreateBundlePromoRuleRequest request,
        CancellationToken cancellationToken = default);
}
