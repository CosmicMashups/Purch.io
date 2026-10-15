namespace Purch.Domain.Enums;

/// <summary>Only Counted lines post an adjustment when the take is applied; Uncounted and Skipped post nothing.</summary>
public enum StockTakeLineState
{
    Uncounted,
    Counted,
    Skipped,
}
