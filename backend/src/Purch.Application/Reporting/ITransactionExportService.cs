namespace Purch.Application.Reporting;

public interface ITransactionExportService
{
    /// <summary>A CSV of every completed sale in the range, one row per transaction — the tenant's own
    /// record of its sales, exportable independently of this app (for accounting, a switch to another
    /// system, or a GDPR/Data Privacy Act access request). branchId scoping follows the same rules as
    /// ISalesDashboardService.GetDashboardAsync. <paramref name="toUtc"/> is exclusive.</summary>
    Task<string> GenerateTransactionsCsvAsync(
        Guid? branchId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default);
}
