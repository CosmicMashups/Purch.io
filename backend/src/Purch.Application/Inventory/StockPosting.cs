using Purch.Application.Catalog;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.Inventory;

/// <summary>The one path that stages a signed stock change for an item or a standalone ingredient together with its
/// InventoryMovement. Shared by stock-take apply and CSV import commit, so both route item stock through
/// <see cref="IItemStockService"/> exactly like a hand-recorded movement.</summary>
internal static class StockPosting
{
    /// <summary>Stages <paramref name="quantity"/> (signed for an Adjustment, positive for a StockIn) against the item or
    /// ingredient and adds the movement. Returns null when the target no longer exists or is deleted.</summary>
    public static async Task<(InventoryMovement Movement, decimal ResultingOnHand)?> PostAsync(
        Guid? itemId,
        Guid? inventoryItemId,
        MovementType type,
        decimal quantity,
        Guid branchId,
        string? note,
        Guid tenantId,
        Guid staffUserId,
        IItemRepository itemRepository,
        IInventoryItemRepository inventoryItemRepository,
        IItemStockService itemStockService,
        IInventoryMovementRepository movementRepository,
        CancellationToken cancellationToken)
    {
        var delta = InventoryMovementService.StockDelta(type, quantity);
        decimal resulting;
        Guid? movementItemId = null;
        Guid? movementInventoryItemId;
        if (inventoryItemId is { } ingredientId)
        {
            var ingredient = await inventoryItemRepository.GetByIdAsync(ingredientId, cancellationToken);
            if (ingredient is null || ingredient.IsDeleted)
            {
                return null;
            }

            ingredient.QuantityOnHand += delta;
            resulting = ingredient.QuantityOnHand;
            movementInventoryItemId = ingredient.Id;
        }
        else
        {
            var item = itemId is { } id ? await itemRepository.GetByIdAsync(id, cancellationToken) : null;
            if (item is null || item.IsDeleted)
            {
                return null;
            }

            // Read before adjusting: the on-hand lookup may read the stored (pre-adjustment) figure.
            resulting = (await itemStockService.GetOnHandAsync([item], tenantId, cancellationToken)).GetValueOrDefault(item.Id) + delta;
            movementItemId = item.Id;
            movementInventoryItemId = await itemStockService.AdjustAsync(item, delta, cancellationToken);
        }

        var movement = new InventoryMovement
        {
            TenantId = tenantId,
            ItemId = movementItemId,
            InventoryItemId = movementInventoryItemId,
            BranchId = branchId,
            Type = type,
            Quantity = quantity,
            StaffUserId = staffUserId,
            Note = note,
        };
        movementRepository.Add(movement);
        return (movement, resulting);
    }
}
