namespace Purch.Application.Reporting;

/// <summary>F1 — deliberately rolling windows ("last 7/30 days"), not
/// calendar week/month boundaries, and computed off UTC "now" rather than
/// the branch's local timezone — both pragmatic simplifications flagged
/// here rather than silently assumed, same spirit as the BIR reading's own
/// documented caveats.</summary>
public sealed record SalesDashboardDto(
    decimal RevenueToday,
    decimal RevenueLast7Days,
    decimal RevenueLast30Days,
    IReadOnlyList<DailyRevenuePointDto> Trend,
    IReadOnlyList<TopSellingItemDto> TopSellingItems,
    IReadOnlyList<BranchRevenueDto> BranchComparison);

public sealed record DailyRevenuePointDto(DateOnly Date, decimal Revenue);

public sealed record TopSellingItemDto(Guid ItemId, string ItemName, decimal QuantitySold, decimal Revenue);

/// <summary>One row per branch within the caller's scope — a single row for a
/// Branch/Department-scoped caller, all tenant branches for a Tenant-scoped one.</summary>
public sealed record BranchRevenueDto(Guid BranchId, string BranchName, decimal Revenue);
