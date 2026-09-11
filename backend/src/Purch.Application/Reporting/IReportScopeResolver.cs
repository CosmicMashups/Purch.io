namespace Purch.Application.Reporting;

/// <summary>Resolves the branch filter every report actually runs against —
/// shared by SalesDashboardService/InventoryReportService/StaffPerformanceService
/// so scope enforcement lives in exactly one place rather than being
/// re-derived per report.</summary>
public interface IReportScopeResolver
{
    /// <summary>Tenant-scoped callers may pass any branchId (or null, for a
    /// consolidated all-branches view); Branch- or Department-scoped callers
    /// are always restricted to their own branch, regardless of what they
    /// request — a crafted branchId query param can't widen what they see.</summary>
    Task<Guid?> ResolveBranchIdAsync(Guid? requestedBranchId, CancellationToken cancellationToken = default);
}
