using Purch.Application.Catalog;
using Purch.Application.Common;
using Purch.Domain.Enums;

namespace Purch.Application.Reporting;

public interface ISalesMixReportService
{
    Task<SalesMixDto> GetReportAsync(Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default);
}

public sealed class SalesMixReportService(
    IReportInsightsRepository insightsRepository,
    ICategoryRepository categoryRepository,
    IReportScopeResolver reportScopeResolver,
    ICurrentTenantProvider currentTenantProvider) : ISalesMixReportService
{
    private const string UncategorisedName = "Uncategorised";
    private const string OpenItemsName = "Open items";

    public async Task<SalesMixDto> GetReportAsync(
        Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default)
    {
        ReportRange.Validate(fromUtc, toUtc);
        var resolvedBranchId = await reportScopeResolver.ResolveBranchIdAsync(branchId, cancellationToken);

        // Sequential on purpose: one scoped DbContext can not run concurrent queries.
        var payments = await insightsRepository.GetPaymentTotalsAsync(resolvedBranchId, fromUtc, toUtc, cancellationToken);
        var categories = await insightsRepository.GetCategoryTotalsAsync(resolvedBranchId, fromUtc, toUtc, cancellationToken);
        var utcHours = await insightsRepository.GetHourlySaleFactsAsync(resolvedBranchId, fromUtc, toUtc, cancellationToken);
        var exchange = await insightsRepository.GetExchangePriceDifferenceAsync(resolvedBranchId, fromUtc, toUtc, cancellationToken);

        var tenantId = currentTenantProvider.TenantId
            ?? throw new InvalidOperationException("The sales mix report requires an authenticated tenant context.");
        var names = (await categoryRepository.ListByTenantAsync(tenantId, cancellationToken))
            .ToDictionary(category => category.Id, category => category.Name);

        // The business runs on a whole-hour offset, so shifting the UTC hour buckets is exact.
        var local = utcHours
            .Select(row =>
            {
                var shifted = new DateTime(row.UtcDate.Year, row.UtcDate.Month, row.UtcDate.Day, row.UtcHour, 0, 0)
                    .Add(ReportTimeZone.Offset);
                return (Date: DateOnly.FromDateTime(shifted), shifted.Hour, Sales: row.Total, TransactionCount: row.SaleCount);
            })
            .ToList();

        var operatingDays = local.Select(row => row.Date).Distinct().Count();
        var hours = Enumerable.Range(0, 24).Select(hour =>
        {
            var rows = local.Where(row => row.Hour == hour).ToList();
            var sales = rows.Sum(row => row.Sales);
            var count = rows.Sum(row => row.TransactionCount);
            return new HourRowDto(
                hour,
                sales,
                count,
                count == 0 ? 0 : Math.Round(sales / count, 2),
                operatingDays == 0 ? 0 : Math.Round(sales / operatingDays, 2));
        }).ToList();

        var heatmap = local
            .GroupBy(row => (Day: (int)row.Date.DayOfWeek, row.Hour))
            .Select(group => new HeatCellDto(group.Key.Day, group.Key.Hour, group.Sum(r => r.Sales), group.Sum(r => r.TransactionCount)))
            .OrderBy(cell => cell.DayOfWeek).ThenBy(cell => cell.Hour)
            .ToList();

        return new SalesMixDto(
            GrossSales: local.Sum(row => row.Sales),
            TransactionCount: local.Sum(row => row.TransactionCount),
            OperatingDays: operatingDays,
            Payments: [.. payments
                .Select(p => new PaymentMixRowDto(p.Method, p.Amount, p.PaymentCount, p.Method == PaymentMethod.UtangCredit))
                .OrderByDescending(p => p.Amount)],
            ExchangeSettlements: exchange,
            Categories: [.. categories
                .Select(c => new CategoryMixRowDto(
                    c.IsOpenItem ? null : c.CategoryId,
                    c.IsOpenItem ? OpenItemsName
                        : c.CategoryId is { } id && names.TryGetValue(id, out var name) ? name : UncategorisedName,
                    c.IsOpenItem,
                    c.Revenue,
                    c.Quantity))
                .OrderByDescending(c => c.Revenue)],
            Hours: hours,
            Heatmap: heatmap);
    }
}
