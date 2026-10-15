namespace Purch.Application.Reporting;

/// <summary>Cost of goods sold from the sale-time cost snapshot on each line. Lines whose cost is unknown
/// (null, never zero) and open items are left out of Cogs and counted in ExcludedLineCount. Quantities are net of
/// refunds and exchange returns. Recipe/ingredient consumption is not added: only the finished item's own snapshot.</summary>
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

/// <summary>One sold line, with what has since been returned against it (a whole-sale refund returns every unit).</summary>
public sealed record CogsLineRow(
    DateTimeOffset SoldAt,
    Guid? ItemId,
    Guid? CategoryId,
    bool IsOpenItem,
    decimal Quantity,
    decimal ReturnedQuantity,
    decimal LineTotal,
    decimal? UnitCost);
