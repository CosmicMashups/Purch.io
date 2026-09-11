namespace Purch.Application.Reporting;

public interface ISalesDashboardService
{
    /// <summary>branchId is optional — a Tenant-scoped caller may pass one for a
    /// per-branch view or omit it for the consolidated all-branches view; a
    /// Branch/Department-scoped caller is restricted to their own branch
    /// regardless (see IReportScopeResolver).</summary>
    Task<SalesDashboardDto> GetDashboardAsync(Guid? branchId, CancellationToken cancellationToken = default);
}
