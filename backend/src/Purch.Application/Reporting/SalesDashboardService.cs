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

    private const int MaxRangeDays = 731;

    public async Task<SalesDashboardDto> GetDashboardAsync(
        Guid? branchId,
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null,
        CancellationToken cancellationToken = default)
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

        // The selected window: whole business days from the start of the first to the start of the day after the
        // last. Without one, the trend is the last 14 days and the lists/total cover the last 30.
        DateTimeOffset windowStart;
        DateTimeOffset windowEnd;
        int trendDays;
        if (fromUtc is { } requestedFrom && toUtc is { } requestedTo && requestedTo > requestedFrom)
        {
            windowStart = ReportTimeZone.StartOfDay(requestedFrom);
            windowEnd = requestedTo;
            trendDays = Math.Clamp((int)Math.Ceiling((windowEnd - windowStart).TotalDays), 1, MaxRangeDays);
            windowEnd = windowStart.AddDays(trendDays) < windowEnd ? windowStart.AddDays(trendDays) : windowEnd;
        }
        else
        {
            windowStart = last30Start;
            windowEnd = to;
            trendDays = TrendDays;
        }

        var trendStart = fromUtc is null || toUtc is null ? todayStart.AddDays(-(TrendDays - 1)) : windowStart;
        var dailyRevenue = (await reportingRepository.GetDailyRevenueAsync(
                resolvedBranchId, trendStart, trendDays, cancellationToken))
            .ToDictionary(d => d.DayIndex, d => d.Revenue);
        var trend = Enumerable.Range(0, trendDays)
            .Select(offset => new DailyRevenuePointDto(
                DateOnly.FromDateTime(trendStart.AddDays(offset).ToOffset(ReportTimeZone.Offset).DateTime),
                dailyRevenue.GetValueOrDefault(offset)))
            .ToList();
        var revenueInRange = fromUtc is null || toUtc is null ? revenueLast30 : dailyRevenue.Values.Sum();

        var topByRevenue = await reportingRepository.GetTopItemsByRevenueAsync(
            resolvedBranchId, windowStart, windowEnd, 10, cancellationToken);
        var topByQuantity = await reportingRepository.GetTopItemsByQuantityAsync(
            resolvedBranchId, windowStart, windowEnd, 10, cancellationToken);
        // Only the listed items' names are needed — loading the whole catalog here cost one query per
        // dashboard view that grew with the tenant's item count instead of staying fixed at 20.
        var items = await itemRepository.ListByIdsAsync(
            topByRevenue.Concat(topByQuantity).Select(top => top.ItemId).Distinct().ToList(), cancellationToken);
        var itemNamesById = items.ToDictionary(item => item.Id, item => item.Name);

        List<TopSellingItemDto> ToDtos(IEnumerable<ItemSalesTotals> rows) => rows
            .Select(top => new TopSellingItemDto(
                top.ItemId,
                itemNamesById.TryGetValue(top.ItemId, out var name) ? name : "(deleted item)",
                top.Quantity,
                top.Revenue))
            .ToList();
        var topSellingItems = ToDtos(topByRevenue);
        var topSellingByQuantity = ToDtos(topByQuantity);

        var allBranches = await branchRepository.ListByTenantAsync(CurrentTenantId, cancellationToken);
        var branches = resolvedBranchId is { } singleBranchId
            ? allBranches.Where(branch => branch.Id == singleBranchId)
            : allBranches;

        var last30ByBranch = branchTotals.ToDictionary(t => t.BranchId, t => t.Last30Days);
        var branchComparison = branches
            .Select(branch => new BranchRevenueDto(branch.Id, branch.Name, last30ByBranch.GetValueOrDefault(branch.Id)))
            .OrderByDescending(dto => dto.Revenue)
            .ToList();

        return new SalesDashboardDto(revenueToday, revenueLast7, revenueLast30, trend, topSellingItems, branchComparison, revenueInRange, topSellingByQuantity);
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("The sales dashboard requires an authenticated tenant context.");
}
