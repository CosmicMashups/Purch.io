namespace Purch.Domain.Enums;

/// <summary>Draft (scope picked, lines materialized) -> Counting -> Review -> Applied; Cancelled from any open state.</summary>
public enum StockTakeStatus
{
    Draft,
    Counting,
    Review,
    Applied,
    Cancelled,
}

/// <summary>Which items a stock take covers; the lines are materialized from it when the take is created.</summary>
public enum StockTakeScopeKind
{
    All,
    Categories,
    Picked,
}
