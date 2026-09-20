using Purch.Application.Catalog;
using Purch.Application.Common;

namespace Purch.Application.Inventory;

public sealed class InventoryDashboardService(
    IItemRepository itemRepository,
    IItemStockService itemStockService,
    ICurrentTenantProvider currentTenantProvider) : IInventoryDashboardService
{
    public async Task<InventoryDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var items = await itemRepository.ListByTenantAsync(CurrentTenantId, cancellationToken);
        var activeItems = items.Where(item => item.IsActive).ToList();

        var onHand = await itemStockService.GetOnHandAsync(activeItems, CurrentTenantId, cancellationToken);

        var outOfStockCount = activeItems.Count(item => onHand[item.Id] <= 0);

        var lowStockItems = activeItems
            .Where(item => item.LowStockThreshold is { } threshold && onHand[item.Id] > 0 && onHand[item.Id] <= threshold)
            .OrderBy(item => onHand[item.Id])
            .Select(item => new LowStockItemDto(item.Id, item.Name, onHand[item.Id], item.LowStockThreshold!.Value))
            .ToList();

        return new InventoryDashboardDto(
            activeItems.Count,
            outOfStockCount,
            lowStockItems.Count,
            lowStockItems);
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("The inventory dashboard requires an authenticated tenant context.");
}
