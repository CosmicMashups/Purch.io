namespace Purch.Application.Inventory;

public interface IInventoryDashboardService
{
    Task<InventoryDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default);
}
