using Purch.Application.Common.Exceptions;
using Purch.Application.Onboarding;
using Purch.Domain.Entities;

namespace Purch.Application.Inventory;

/// <summary>Where an item's stock count lives. A tenant that has opted into
/// UseSeparateInventoryTracking keeps stock on the item's auto-paired InventoryItem (the record
/// POS sales draw down and IsOutOfStock reads), so every other stock change and stock report has to
/// use that same record — otherwise deliveries, adjustments and transfers move a counter the
/// cashier never looks at. Everyone else, and an item with no paired InventoryItem, keeps using
/// Item.StockOnHand.</summary>
public interface IItemStockService
{
    /// <summary>Stages <paramref name="delta"/> against the item's stock count. Returns the
    /// InventoryItem id when the change was routed there, so the caller can record it on the
    /// movement, or null when Item.StockOnHand was changed.</summary>
    Task<Guid?> AdjustAsync(Item item, decimal delta, CancellationToken cancellationToken = default);

    /// <summary>The current on-hand quantity of each item, keyed by item id. For a tenant using
    /// separate tracking, items made from a recipe are left out: they hold no stock of their own
    /// (their availability comes from their ingredients).</summary>
    Task<IReadOnlyDictionary<Guid, decimal>> GetOnHandAsync(IReadOnlyCollection<Item> items, Guid tenantId, CancellationToken cancellationToken = default);
}

public sealed class ItemStockService(
    ITenantRepository tenantRepository,
    IInventoryItemRepository inventoryItemRepository,
    IItemRecipeRepository recipeRepository) : IItemStockService
{
    public async Task<Guid?> AdjustAsync(Item item, decimal delta, CancellationToken cancellationToken = default)
    {
        var tenant = await tenantRepository.GetByIdAsync(item.TenantId, cancellationToken);
        if (tenant is { UseSeparateInventoryTracking: true })
        {
            var linked = await inventoryItemRepository.GetByLinkedItemIdAsync(item.TenantId, item.Id, cancellationToken);
            if (linked is not null)
            {
                linked.QuantityOnHand += delta;
                return linked.Id;
            }

            if ((await recipeRepository.ListByItemAsync(item.Id, cancellationToken)).Count > 0)
            {
                throw new ValidationException(
                    nameof(item.Id),
                    $"{item.Name} is made from a recipe, so it has no stock of its own. Change the stock of its ingredients instead.");
            }
        }

        item.StockOnHand += delta;
        return null;
    }

    public async Task<IReadOnlyDictionary<Guid, decimal>> GetOnHandAsync(IReadOnlyCollection<Item> items, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var onHand = items.ToDictionary(item => item.Id, item => item.StockOnHand);

        var tenant = await tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        if (tenant is { UseSeparateInventoryTracking: true })
        {
            foreach (var recipeLine in await recipeRepository.ListByTenantAsync(tenantId, cancellationToken))
            {
                _ = onHand.Remove(recipeLine.ItemId);
            }

            foreach (var inventoryItem in await inventoryItemRepository.ListByTenantAsync(tenantId, cancellationToken))
            {
                if (inventoryItem.LinkedItemId is { } itemId && onHand.ContainsKey(itemId))
                {
                    onHand[itemId] = inventoryItem.QuantityOnHand;
                }
            }
        }

        return onHand;
    }
}
