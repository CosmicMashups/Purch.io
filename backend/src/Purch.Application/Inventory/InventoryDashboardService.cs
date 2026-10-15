using Purch.Application.Catalog;
using Purch.Application.Common;
using Purch.Application.Onboarding;

namespace Purch.Application.Inventory;

public sealed class InventoryDashboardService(
    IItemRepository itemRepository,
    IItemStockService itemStockService,
    IInventoryItemRepository inventoryItemRepository,
    ITenantRepository tenantRepository,
    ICurrentTenantProvider currentTenantProvider) : IInventoryDashboardService
{
    public async Task<InventoryDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var items = await itemRepository.ListByTenantAsync(CurrentTenantId, cancellationToken);
        var activeItems = items.Where(item => item.IsActive).ToList();

        var onHand = await itemStockService.GetOnHandAsync(activeItems, CurrentTenantId, cancellationToken);

        // Recipe items hold no stock of their own; their ingredients carry the counts.
        var counted = activeItems.Where(item => onHand.ContainsKey(item.Id)).ToList();
        var outOfStockCount = counted.Count(item => onHand[item.Id] <= 0);

        // The restock list: out-of-stock items first (even with no threshold), then those at or under their warning level.
        var lowStockItems = counted
            .Where(item => onHand[item.Id] <= 0 || (item.LowStockThreshold is { } threshold && onHand[item.Id] <= threshold))
            .OrderBy(item => onHand[item.Id])
            .Select(item => new LowStockItemDto(item.Id, item.Name, onHand[item.Id], item.LowStockThreshold ?? 0m))
            .ToList();
        var lowCount = lowStockItems.Count(item => item.StockOnHand > 0);

        return new InventoryDashboardDto(
            activeItems.Count,
            outOfStockCount,
            lowCount,
            lowStockItems,
            await GetIngredientsAsync(cancellationToken));
    }

    private async Task<IngredientStockDto?> GetIngredientsAsync(CancellationToken cancellationToken)
    {
        var tenant = await tenantRepository.GetByIdAsync(CurrentTenantId, cancellationToken);
        if (tenant is not { UseSeparateInventoryTracking: true })
        {
            return null;
        }

        var ingredients = (await inventoryItemRepository.ListByTenantAsync(CurrentTenantId, cancellationToken))
            .Where(ingredient => ingredient.IsActive && !ingredient.IsAutoCreatedForItem)
            .ToList();

        var low = ingredients
            .Where(ingredient => ingredient.QuantityOnHand <= 0 || (ingredient.LowStockThreshold is { } threshold && ingredient.QuantityOnHand <= threshold))
            .OrderBy(ingredient => ingredient.QuantityOnHand)
            .Select(ingredient => new LowStockIngredientDto(ingredient.Id, ingredient.Name, ingredient.BaseUnit, ingredient.QuantityOnHand, ingredient.LowStockThreshold ?? 0m))
            .ToList();

        return new IngredientStockDto(ingredients.Count, ingredients.Count(ingredient => ingredient.QuantityOnHand <= 0), low.Count(i => i.QuantityOnHand > 0), low);
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("The inventory dashboard requires an authenticated tenant context.");
}
