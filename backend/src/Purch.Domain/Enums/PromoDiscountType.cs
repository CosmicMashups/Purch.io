namespace Purch.Domain.Enums;

public enum PromoDiscountType
{
    Percentage,
    FixedAmount,

    /// <summary>The item is priced at exactly DiscountValue while the rule is active.
    /// Appended (not inserted) since this enum is persisted as an int.</summary>
    FixedPrice,
}
