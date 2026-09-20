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

    /// <summary>The current on-hand quantity of each item, keyed by item id.</summary>
    Task<IReadOnlyDictionary<Guid, decimal>> GetOnHandAsync(IReadOnlyCollection<Item> items, Guid tenantId, CancellationToken cancellationToken = default);
}

public sealed class ItemStockService(
    ITenantRepository tenantRepository,
    IInventoryItemRepository inventoryItemRepository) : IItemStockService
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
