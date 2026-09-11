using System.Text.Json;
using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.Catalog;

public sealed class ItemVariantService(
    IItemVariantRepository itemVariantRepository,
    IItemRepository itemRepository,
    ICurrentTenantProvider currentTenantProvider,
    IUnitOfWork unitOfWork) : IItemVariantService
{
    public async Task<IReadOnlyList<ItemVariantDto>> ListForItemAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        _ = await itemRepository.RequirePricingTypeAsync(itemId, PricingType.VariantMatrix, cancellationToken);

        var variants = await itemVariantRepository.ListByItemAsync(itemId, cancellationToken);
        return [.. variants.Select(ToDto)];
    }

    public async Task<ItemVariantDto> CreateAsync(
        Guid itemId,
        CreateItemVariantRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Attributes.Count == 0)
        {
            throw new ValidationException(
                nameof(request.Attributes),
                "At least one attribute is required (e.g. size, color).");
        }

        if (request.PriceOverride is < 0)
        {
            throw new ValidationException(nameof(request.PriceOverride), "Price override cannot be negative.");
        }

        _ = await itemRepository.RequirePricingTypeAsync(itemId, PricingType.VariantMatrix, cancellationToken);

        var variant = new ItemVariant
        {
            TenantId = CurrentTenantId,
            ItemId = itemId,
            VariantAttributesJson = JsonSerializer.Serialize(request.Attributes),
            Sku = request.Sku?.Trim(),
            PriceOverride = request.PriceOverride,
            ImageUrl = request.ImageUrl,
        };

        itemVariantRepository.Add(variant);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(variant);
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("Variant management requires an authenticated tenant context.");

    private static ItemVariantDto ToDto(ItemVariant variant)
    {
        var attributes = JsonSerializer.Deserialize<Dictionary<string, string>>(variant.VariantAttributesJson)
            ?? [];

        return new(
            variant.Id,
            attributes,
            variant.Sku,
            variant.StockOnHand,
            variant.PriceOverride,
            variant.ImageUrl);
    }
}
