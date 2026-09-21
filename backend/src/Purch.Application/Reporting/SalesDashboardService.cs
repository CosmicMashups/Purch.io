using Purch.Application.Catalog;
using Purch.Application.Common;
using Purch.Application.Onboarding;

namespace Purch.Application.Reporting;

public sealed class SalesDashboardService(
    IReportingRepository reportingRepository,
    IBranchRepository branchRepository,
    IItemRepository itemRepository,
    IReportScopeResolver reportScopeResolver,
    ICurrentTenantProvider currentTenantProvider) : ISalesDashboardService
{
    private const int TrendDays = 14;

    public async Task<SalesDashboardDto> GetDashboardAsync(Guid? branchId, CancellationToken cancellationToken = default)
    {
        var resolvedBranchId = await reportScopeResolver.ResolveBranchIdAsync(branchId, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var todayStart = ReportTimeZone.StartOfDay(now);
        var last7Start = todayStart.AddDays(-6);
        var last30Start = todayStart.AddDays(-29);
        var to = now.AddTicks(1);

        // Everything below is aggregated in SQL; only the small grouped results come back.
        var branchTotals = await reportingRepository.GetBranchRevenueTotalsAsync(
            resolvedBranchId, todayStart, last7Start, last30Start, to, cancellationToken);

        var revenueToday = branchTotals.Sum(t => t.Today);
        var revenueLast7 = branchTotals.Sum(t => t.Last7Days);
        var revenueLast30 = branchTotals.Sum(t => t.Last30Days);

        var trendStart = todayStart.AddDays(-(TrendDays - 1));
        var dailyRevenue = (await reportingRepository.GetDailyRevenueAsync(
                resolvedBranchId, trendStart, TrendDays, cancellationToken))
            .ToDictionary(d => d.DayIndex, d => d.Revenue);
        var trend = Enumerable.Range(0, TrendDays)
            .Select(offset => new DailyRevenuePointDto(
                DateOnly.FromDateTime(trendStart.AddDays(offset).ToOffset(ReportTimeZone.Offset).DateTime),
                dailyRevenue.GetValueOrDefault(offset)))
            .ToList();

        var topItems = await reportingRepository.GetTopItemsByRevenueAsync(
            resolvedBranchId, last30Start, to, 10, cancellationToken);
        var items = await itemRepository.ListByTenantAsync(CurrentTenantId, cancellationToken);
        var itemNamesById = items.ToDictionary(item => item.Id, item => item.Name);

        var topSellingItems = topItems
            .Select(top => new TopSellingItemDto(
                top.ItemId,
                itemNamesById.TryGetValue(top.ItemId, out var name) ? name : "(deleted item)",
                top.Quantity,
                top.Revenue))
            .ToList();

        var allBranches = await branchRepository.ListByTenantAsync(CurrentTenantId, cancellationToken);
        var branches = resolvedBranchId is { } singleBranchId
            ? allBranches.Where(branch => branch.Id == singleBranchId)
            : allBranches;

        var last30ByBranch = branchTotals.ToDictionary(t => t.BranchId, t => t.Last30Days);
        var branchComparison = branches
            .Select(branch => new BranchRevenueDto(branch.Id, branch.Name, last30ByBranch.GetValueOrDefault(branch.Id)))
            .OrderByDescending(dto => dto.Revenue)
            .ToList();

        return new SalesDashboardDto(revenueToday, revenueLast7, revenueLast30, trend, topSellingItems, branchComparison);
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("The sales dashboard requires an authenticated tenant context.");
}
