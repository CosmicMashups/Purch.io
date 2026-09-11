namespace Purch.Application.Reporting;

public interface IDepartmentSalesReportService
{
    /// <summary>branchId scoping follows the same rules as
    /// ISalesDashboardService.GetDashboardAsync.</summary>
    Task<IReadOnlyList<DepartmentSalesSummaryDto>> GetReportAsync(
        Guid? branchId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default);
}
