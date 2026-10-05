using Purch.Application.Common;
using Purch.Application.Inventory;
using Purch.Application.Onboarding;
using Purch.Domain.Entities;

namespace Purch.Application.Catalog;

/// <summary>Whether a modifier can be chosen right now. Mirrors how an item's recipe decides availability:
/// an ingredient that is depleted, or has less left than one more selection would use, blocks the modifier.</summary>
public static class ModifierAvailability
{
    public static bool IsOutOfStock(IEnumerable<ItemModifierIngredient> ingredients, IReadOnlyDictionary<Guid, InventoryItem> inventoryItemsById)
    {
        foreach (var ingredient in ingredients)
        {
            // An ingredient that was deleted no longer limits anything.
            if (!inventoryItemsById.TryGetValue(ingredient.InventoryItemId, out var inventoryItem))
            {
                continue;
            }

            if (inventoryItem.QuantityOnHand <= 0)
            {
                return true;
            }

            if (ingredient.QuantityPerOrder is { } quantityPerOrder && inventoryItem.QuantityOnHand < quantityPerOrder)
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>Turns modifier groups into the DTOs clients read, adding each modifier's ingredients and
/// availability in one batch (three queries for the whole catalog, not per modifier). Availability is only
/// worked out when the business tracks inventory separately; otherwise nothing limits a modifier.</summary>
public sealed class ModifierDtoBuilder(
    ITenantRepository tenantRepository,
    IInventoryItemRepository inventoryItemRepository,
    IItemModifierIngredientRepository ingredientRepository,
    ICurrentTenantProvider currentTenantProvider)
{
    public async Task<IReadOnlyList<ModifierGroupDto>> BuildAsync(
        IEnumerable<(ModifierGroup Group, IReadOnlyList<ItemModifier> Modifiers)> groups,
        CancellationToken cancellationToken = default)
    {
        var tenantId = currentTenantProvider.TenantId
            ?? throw new InvalidOperationException("Modifier groups require an authenticated tenant context.");

        var tenant = await tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        var tracksInventory = tenant is { UseSeparateInventoryTracking: true };

        var ingredients = await ingredientRepository.ListByTenantAsync(tenantId, cancellationToken);
        var ingredientsByModifier = ingredients.ToLookup(ingredient => ingredient.ItemModifierId);

        var inventoryById = ingredients.Count == 0
            ? new Dictionary<Guid, InventoryItem>()
            : (await inventoryItemRepository.ListByIdsAsync([.. ingredients.Select(i => i.InventoryItemId).Distinct()], cancellationToken))
                .ToDictionary(inventoryItem => inventoryItem.Id);

        return [.. groups.Select(pair => new ModifierGroupDto(
            pair.Group.Id,
            pair.Group.Name,
            pair.Group.AllowMultipleSelection,
            pair.Group.IsRequired,
            [.. pair.Modifiers.Select(modifier => new ItemModifierDto(
                modifier.Id,
                modifier.Name,
                modifier.PriceDelta,
                tracksInventory && ModifierAvailability.IsOutOfStock(ingredientsByModifier[modifier.Id], inventoryById),
                [.. ingredientsByModifier[modifier.Id].Select(ingredient => new ModifierIngredientDto(
                    ingredient.InventoryItemId,
                    inventoryById.TryGetValue(ingredient.InventoryItemId, out var inventoryItem) ? inventoryItem.Name : "(deleted inventory item)",
                    ingredient.QuantityPerOrder))]))]))];
    }
}
