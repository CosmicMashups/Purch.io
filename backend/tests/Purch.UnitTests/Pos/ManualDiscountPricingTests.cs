using Purch.Application.Pos;
using Purch.Domain.Enums;

namespace Purch.UnitTests.Pos;

public sealed class ManualDiscountPricingTests
{
    [Fact]
    public void Ten_percent_on_a_200_line_is_20_off_and_vat_is_taken_on_the_net()
    {
        var amount = ManualDiscountPricing.Amount(ManualDiscountKind.Percent, 10m, 200m);

        Assert.Equal(20m, amount);
        var vat = VatBreakdownCalculator.Compute(200m - amount, 0m);
        Assert.Equal(160.71m, vat.VatableSales);
        Assert.Equal(19.29m, vat.VatAmount);
    }

    [Fact]
    public void A_fixed_500_on_a_200_line_is_clamped_to_200_and_never_negative()
    {
        var amount = ManualDiscountPricing.Amount(ManualDiscountKind.Fixed, 500m, 200m);
        Assert.Equal(200m, amount);

    }

    [Fact]
    public void A_cart_percent_is_taken_after_the_line_discounts_and_each_applies_once()
    {
        var lineAmount = ManualDiscountPricing.Amount(ManualDiscountKind.Fixed, 50m, 200m);
        Assert.Equal(50m, lineAmount);

        var netAfterLines = 500m - lineAmount;
        var cartAmount = ManualDiscountPricing.Amount(ManualDiscountKind.Percent, 10m, netAfterLines);
        Assert.Equal(45m, cartAmount);

        var resolved = ManualDiscountPricing.ResolveCart(true, cartAmount, netAfterLines, itemPromoAmount: 0m, promoCodeAmount: 0m);
        Assert.Equal(45m, resolved.CartAmount);
    }

    [Fact]
    public void A_manual_cart_discount_suppresses_the_promo_code_and_item_promos()
    {
        var resolved = ManualDiscountPricing.ResolveCart(true, 30m, 500m, itemPromoAmount: 40m, promoCodeAmount: 100m);

        Assert.Equal(30m, resolved.CartAmount);
        Assert.Equal(0m, resolved.AppliedPromoCode);
        Assert.Equal(0m, resolved.AppliedItemPromo);
    }

    [Fact]
    public void Without_a_manual_cart_discount_the_larger_promotion_still_wins_and_a_tie_goes_to_item_promos()
    {
        var code = ManualDiscountPricing.ResolveCart(false, 0m, 500m, itemPromoAmount: 40m, promoCodeAmount: 100m);
        Assert.Equal(100m, code.AppliedPromoCode);
        Assert.Equal(0m, code.AppliedItemPromo);

        var tie = ManualDiscountPricing.ResolveCart(false, 0m, 500m, itemPromoAmount: 40m, promoCodeAmount: 40m);
        Assert.Equal(40m, tie.AppliedItemPromo);
        Assert.Equal(0m, tie.AppliedPromoCode);
    }

    [Fact]
    public void A_cart_discount_larger_than_what_is_left_is_clamped()
    {
        var resolved = ManualDiscountPricing.ResolveCart(true, 900m, 450m, 0m, 0m);
        Assert.Equal(450m, resolved.CartAmount);
    }

    [Fact]
    public void The_amount_is_derived_from_kind_and_value_so_it_re_expands_when_the_base_grows()
    {
        // 10% of 100 then of 300: a derived amount follows the base, nothing is ratcheted.
        Assert.Equal(10m, ManualDiscountPricing.Amount(ManualDiscountKind.Percent, 10m, 100m));
        Assert.Equal(30m, ManualDiscountPricing.Amount(ManualDiscountKind.Percent, 10m, 300m));
        // A fixed 150 clamps on a 100 base and is whole again on a 300 base.
        Assert.Equal(100m, ManualDiscountPricing.Amount(ManualDiscountKind.Fixed, 150m, 100m));
        Assert.Equal(150m, ManualDiscountPricing.Amount(ManualDiscountKind.Fixed, 150m, 300m));
    }
}
