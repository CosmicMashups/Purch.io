using Purch.Domain.Enums;

namespace Purch.Application.Pos;

/// <summary>
/// Pure arithmetic for the Manager/Admin manual discount. Line discounts are taken first, the cart discount is
/// then taken from what is left (never off a line twice), nothing can exceed what it covers, and a manual
/// discount suppresses item promos and the promo code. Senior/PWD exclusivity is decided by the caller.
/// </summary>
public static class ManualDiscountPricing
{
    public sealed record CartResolution(decimal CartAmount, decimal AppliedItemPromo, decimal AppliedPromoCode);

    /// <summary>The discount for <paramref name="value"/> of <paramref name="kind"/> on a base, clamped to [0, base].
    /// The clamp is applied here, at compute time only: the stored kind/value are never rewritten.</summary>
    public static decimal Amount(ManualDiscountKind kind, decimal value, decimal baseAmount)
    {
        var raw = kind == ManualDiscountKind.Percent
            ? Math.Round(baseAmount * value / 100m, 2, MidpointRounding.AwayFromZero)
            : Math.Round(value, 2, MidpointRounding.AwayFromZero);
        return Math.Max(0m, Math.Min(raw, Math.Max(0m, baseAmount)));
    }

    /// <summary>
    /// Settles the cart-level discount against the promotions. <paramref name="netAfterLineDiscounts"/> is the
    /// subtotal after line discounts. With a manual cart discount, every promotion is suppressed; otherwise the
    /// single larger promotion applies (a tie goes to the item promos), exactly as before.
    /// </summary>
    public static CartResolution ResolveCart(bool cartDiscountPresent, decimal cartAmount, decimal netAfterLineDiscounts, decimal itemPromoAmount, decimal promoCodeAmount)
    {
        if (cartDiscountPresent)
        {
            return new CartResolution(Math.Max(0m, Math.Min(cartAmount, netAfterLineDiscounts)), 0m, 0m);
        }

        return itemPromoAmount >= promoCodeAmount
            ? new CartResolution(0m, itemPromoAmount, 0m)
            : new CartResolution(0m, 0m, promoCodeAmount);
    }
}
