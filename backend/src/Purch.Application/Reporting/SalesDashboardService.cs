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
    public async Task<SalesDashboardDto> GetDashboardAsync(Guid? branchId, CancellationToken cancellationToken = default)
    {
        var resolvedBranchId = await reportScopeResolver.ResolveBranchIdAsync(branchId, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var todayStart = new DateTimeOffset(now.Date, TimeSpan.Zero);
        var last7Start = todayStart.AddDays(-6);
        var last30Start = todayStart.AddDays(-29);

        // The 30-day window is a superset of every other window this dashboard
        // needs, so one fetch covers today/last7/last30/trend/top-items/branch-comparison.
        var transactions = await reportingRepository.ListCompletedTransactionsAsync(
            resolvedBranchId, last30Start, now.AddTicks(1), cancellationToken);

        var revenueToday = transactions.Where(t => t.CreatedAt >= todayStart).Sum(t => t.TotalAmount);
        var revenueLast7 = transactions.Where(t => t.CreatedAt >= last7Start).Sum(t => t.TotalAmount);
        var revenueLast30 = transactions.Sum(t => t.TotalAmount);

        var trend = Enumerable.Range(0, 14)
            .Select(offset => todayStart.AddDays(-13 + offset))
            .Select(day => new DailyRevenuePointDto(
                DateOnly.FromDateTime(day.UtcDateTime),
                transactions.Where(t => t.CreatedAt >= day && t.CreatedAt < day.AddDays(1)).Sum(t => t.TotalAmount)))
            .ToList();

        var lines = await reportingRepository.ListLinesForTransactionsAsync(
            [.. transactions.Select(t => t.Id)], cancellationToken);

        var items = await itemRepository.ListByTenantAsync(CurrentTenantId, cancellationToken);
        var itemNamesById = items.ToDictionary(item => item.Id, item => item.Name);

        var topSellingItems = lines
            .GroupBy(line => line.ItemId)
            .Select(group => new TopSellingItemDto(
                group.Key,
                itemNamesById.TryGetValue(group.Key, out var name) ? name : "(deleted item)",
                group.Sum(line => line.Quantity),
                group.Sum(line => line.LineTotal)))
            .OrderByDescending(dto => dto.Revenue)
            .Take(10)
            .ToList();

        var allBranches = await branchRepository.ListByTenantAsync(CurrentTenantId, cancellationToken);
        var branches = resolvedBranchId is { } singleBranchId
            ? allBranches.Where(branch => branch.Id == singleBranchId)
            : allBranches;

        var branchComparison = branches
            .Select(branch => new BranchRevenueDto(
                branch.Id,
                branch.Name,
                transactions.Where(t => t.BranchId == branch.Id).Sum(t => t.TotalAmount)))
            .OrderByDescending(dto => dto.Revenue)
            .ToList();

        return new SalesDashboardDto(revenueToday, revenueLast7, revenueLast30, trend, topSellingItems, branchComparison);
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("The sales dashboard requires an authenticated tenant context.");
}
