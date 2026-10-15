namespace Purch.Application.Reporting;

/// <summary>Type is item, ingredient or all.</summary>
public sealed record InventoryValuationQuery(Guid? CategoryId, string Type = "all");

public sealed record ValuationRowDto(
    Guid Id,
    string Kind,
    string Name,
    string? Sku,
    string? Category,
    decimal OnHand,
    decimal? UnitCost,
    decimal? Value,
    bool Flagged);

public sealed record ValuationGroupDto(Guid? CategoryId, string Name, decimal Subtotal, IReadOnlyList<ValuationRowDto> Rows);

public sealed record InventoryValuationDto(
    IReadOnlyList<ValuationGroupDto> Groups,
    decimal TotalValue,
    int ValuedCount,
    int UnknownCostCount,
    int NegativeStockCount);
