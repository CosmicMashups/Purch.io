namespace Purch.Application.Reporting;

public interface ISalesDashboardService
{
    /// <summary>branchId is optional — a Tenant-scoped caller may pass one for a
    /// per-branch view or omit it for the consolidated all-branches view; a
    /// Branch/Department-scoped caller is restricted to their own branch
    /// regardless (see IReportScopeResolver).</summary>
    /// <remarks>fromUtc/toUtc (exclusive) optionally set the window the trend, range revenue and top-seller
    /// lists cover; without them the window is the last 30 days (14-day trend).</remarks>
    Task<SalesDashboardDto> GetDashboardAsync(
        Guid? branchId,
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null,
        CancellationToken cancellationToken = default);
}
