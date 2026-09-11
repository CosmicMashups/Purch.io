namespace Purch.Application.Reporting;

public interface IStaffPerformanceService
{
    /// <summary>branchId scoping follows the same rules as
    /// ISalesDashboardService.GetDashboardAsync.</summary>
    Task<StaffPerformanceReportDto> GetReportAsync(
        Guid? branchId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default);
}
