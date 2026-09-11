namespace Purch.Application.Catalog;

/// <summary>A null Threshold clears the alert — StockOnHand at or below a set
/// threshold (and above zero) triggers C1's low-stock dashboard alert.</summary>
public sealed record UpdateLowStockThresholdRequest(decimal? Threshold);
