using Purch.Application.Catalog;
using Purch.Application.Common;

namespace Purch.Application.Inventory;

public sealed class InventoryDashboardService(
    IItemRepository itemRepository,
    ICurrentTenantProvider currentTenantProvider) : IInventoryDashboardService
{
    public async Task<InventoryDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var items = await itemRepository.ListByTenantAsync(CurrentTenantId, cancellationToken);
        var activeItems = items.Where(item => item.IsActive).ToList();

        var outOfStockCount = activeItems.Count(item => item.StockOnHand <= 0);

        var lowStockItems = activeItems
            .Where(item => item.LowStockThreshold is { } threshold && item.StockOnHand > 0 && item.StockOnHand <= threshold)
            .OrderBy(item => item.StockOnHand)
            .Select(item => new LowStockItemDto(item.Id, item.Name, item.StockOnHand, item.LowStockThreshold!.Value))
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
