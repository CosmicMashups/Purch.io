using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.Catalog;

public sealed class BundlePromoRuleService(
    IBundlePromoRuleRepository bundlePromoRuleRepository,
    IItemRepository itemRepository,
    ICurrentTenantProvider currentTenantProvider,
    IUnitOfWork unitOfWork) : IBundlePromoRuleService
{
    public async Task<IReadOnlyList<BundlePromoRuleDto>> ListForItemAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        _ = await itemRepository.RequirePricingTypeAsync(itemId, PricingType.Bundle, cancellationToken);

        var rules = await bundlePromoRuleRepository.ListByItemAsync(itemId, cancellationToken);
        return [.. rules.Select(ToDto)];
    }

    public async Task<BundlePromoRuleDto> CreateAsync(
        Guid itemId,
        CreateBundlePromoRuleRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Description))
        {
            throw new ValidationException(nameof(request.Description), "A description is required (e.g. \"Buy 2 Get 1\").");
        }

        if (request.TriggerQuantity < 2)
        {
            throw new ValidationException(nameof(request.TriggerQuantity), "A bundle needs at least 2 units to make sense.");
        }

        if (request.BundlePrice < 0)
        {
            throw new ValidationException(nameof(request.BundlePrice), "Bundle price cannot be negative.");
        }

        _ = await itemRepository.RequirePricingTypeAsync(itemId, PricingType.Bundle, cancellationToken);

        var rule = new BundlePromoRule
        {
            TenantId = CurrentTenantId,
            ItemId = itemId,
            Description = request.Description.Trim(),
            TriggerQuantity = request.TriggerQuantity,
            BundlePrice = request.BundlePrice,
            IsActive = true,
        };

        bundlePromoRuleRepository.Add(rule);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(rule);
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("Bundle rule management requires an authenticated tenant context.");

    private static BundlePromoRuleDto ToDto(BundlePromoRule rule)
    {
        return new(rule.Id, rule.Description, rule.TriggerQuantity, rule.BundlePrice, rule.IsActive);
    }
}
