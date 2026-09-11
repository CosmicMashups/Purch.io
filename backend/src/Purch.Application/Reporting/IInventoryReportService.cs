namespace Purch.Application.Reporting;

public interface IInventoryReportService
{
    /// <summary>F3's movement summary by type. branchId scoping follows the
    /// same rules as ISalesDashboardService.GetDashboardAsync.</summary>
    Task<MovementSummaryDto> GetMovementSummaryAsync(
        Guid? branchId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default);

    /// <summary>F3's low-stock/reorder export, as a ready-to-download CSV string.
    /// No branch filter — Item.StockOnHand is a single tenant-wide total (see
    /// C4's branch-transfer notes), so there's no branch dimension to scope by.</summary>
    Task<string> GenerateLowStockReorderCsvAsync(CancellationToken cancellationToken = default);
}
