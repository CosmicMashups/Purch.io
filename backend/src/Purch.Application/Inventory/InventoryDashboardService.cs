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

        var lowStockItems = counted
            .Where(item => item.LowStockThreshold is { } threshold && onHand[item.Id] > 0 && onHand[item.Id] <= threshold)
            .OrderBy(item => onHand[item.Id])
            .Select(item => new LowStockItemDto(item.Id, item.Name, onHand[item.Id], item.LowStockThreshold!.Value))
            .ToList();

        return new InventoryDashboardDto(
            activeItems.Count,
            outOfStockCount,
            lowStockItems.Count,
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
            .Where(ingredient => ingredient.LowStockThreshold is { } threshold && ingredient.QuantityOnHand > 0 && ingredient.QuantityOnHand <= threshold)
            .OrderBy(ingredient => ingredient.QuantityOnHand)
            .Select(ingredient => new LowStockIngredientDto(ingredient.Id, ingredient.Name, ingredient.BaseUnit, ingredient.QuantityOnHand, ingredient.LowStockThreshold!.Value))
            .ToList();

        return new IngredientStockDto(ingredients.Count, ingredients.Count(ingredient => ingredient.QuantityOnHand <= 0), low.Count, low);
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("The inventory dashboard requires an authenticated tenant context.");
}
