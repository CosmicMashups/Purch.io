namespace Purch.Application.Pos;

/// <summary>
/// Pure arithmetic for the Manager/Admin manual discount. Line discounts are taken first, the cart discount is
/// then taken from what is left (never off a line twice), nothing can exceed what it covers, and a manual
/// discount suppresses item promos and the promo code. Senior/PWD exclusivity is decided by the caller.
/// </summary>
public static class ManualDiscountPricing
{
    public const string Percent = "Percent";
    public const string Fixed = "Fixed";

    public readonly record struct LineInput(Guid Id, decimal LineTotal, decimal StoredAmount);

    public sealed record CartResolution(decimal CartAmount, decimal AppliedItemPromo, decimal AppliedPromoCode);

    /// <summary>The discount for <paramref name="value"/> of <paramref name="kind"/> on a base, clamped to [0, base].</summary>
    public static decimal Amount(string kind, decimal value, decimal baseAmount)
    {
        var raw = string.Equals(kind, Percent, StringComparison.OrdinalIgnoreCase)
            ? Math.Round(baseAmount * value / 100m, 2, MidpointRounding.AwayFromZero)
            : Math.Round(value, 2, MidpointRounding.AwayFromZero);
        return Math.Max(0m, Math.Min(raw, Math.Max(0m, baseAmount)));
    }

    /// <summary>Each line's stored manual amount, held to the line's current total.</summary>
    public static IReadOnlyDictionary<Guid, decimal> ClampLines(IEnumerable<LineInput> lines)
        => lines.ToDictionary(line => line.Id, line => Math.Max(0m, Math.Min(line.StoredAmount, Math.Max(0m, line.LineTotal))));

    /// <summary>
    /// Settles the cart-level discount against the promotions. <paramref name="netAfterLineDiscounts"/> is the
    /// subtotal after line discounts. With a manual cart discount, every promotion is suppressed; otherwise the
    /// single larger promotion applies (a tie goes to the item promos), exactly as before.
    /// </summary>
    public static CartResolution ResolveCart(decimal storedCartAmount, decimal netAfterLineDiscounts, decimal itemPromoAmount, decimal promoCodeAmount)
    {
        if (storedCartAmount > 0m)
        {
            return new CartResolution(Math.Max(0m, Math.Min(storedCartAmount, netAfterLineDiscounts)), 0m, 0m);
        }

        return itemPromoAmount >= promoCodeAmount
            ? new CartResolution(0m, itemPromoAmount, 0m)
            : new CartResolution(0m, 0m, promoCodeAmount);
    }
}
