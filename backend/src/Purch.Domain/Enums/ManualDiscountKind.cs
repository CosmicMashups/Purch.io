namespace Purch.Domain.Enums;

/// <summary>How a manual discount's value is read. Append only: stored as an integer.</summary>
public enum ManualDiscountKind
{
    Percent,
    Fixed,
}
