using Purch.Application.Common.Exceptions;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.Catalog;

/// <summary>Shared by every pricing-type-gated sub-resource (batches, bundle rules, variants, ...).</summary>
internal static class PricingTypeValidation
{
    public static async Task<Item> RequirePricingTypeAsync(
        this IItemRepository itemRepository,
        Guid itemId,
        PricingType expected,
        CancellationToken cancellationToken)
    {
        var item = await itemRepository.GetByIdAsync(itemId, cancellationToken)
            ?? throw new NotFoundException("Item", itemId);

        return item.PricingType != expected
            ? throw new ValidationException("ItemId", $"This action requires a {expected}-priced item.")
            : item;
    }
}
