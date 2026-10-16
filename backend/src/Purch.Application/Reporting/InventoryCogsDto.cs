namespace Purch.Application.Reporting;

/// <summary>Cost of goods sold from the sale-time cost snapshot on each line. Lines whose cost is unknown
/// (null, never zero) and open items are left out of Cogs and counted in ExcludedLineCount. Quantities are net of
/// refunds and exchange returns; a refund or return nets against the original sale date, not the refund date. Recipe/ingredient consumption is not added: only the finished item's own snapshot.</summary>
public sealed record CogsGroupDto(
    Guid? Id,
    string Name,
    decimal Quantity,
    decimal Cogs,
    decimal KnownCostRevenue,
    decimal CoveragePercent,
    int ExcludedLineCount);

public sealed record InventoryCogsDto(
    IReadOnlyList<CogsGroupDto> Groups,
    decimal Cogs,
    decimal KnownCostRevenue,
    decimal CoveragePercent,
    int ExcludedLineCount);

/// <summary>The sold lines of one day/item/category/open-item bucket, already summed: Quantity is net of returns, Revenue is what the
/// customer paid for those net units, Cogs and KnownRevenue cover only lines with a known cost (open items never count), and
/// ExcludedLines counts the rest. Lines with nothing left after returns are left out.</summary>
public sealed record CogsAggregateRow(
    string Day,
    Guid? ItemId,
    Guid? CategoryId,
    bool IsOpenItem,
    decimal Quantity,
    decimal Revenue,
    decimal Cogs,
    decimal KnownRevenue,
    int ExcludedLines);
