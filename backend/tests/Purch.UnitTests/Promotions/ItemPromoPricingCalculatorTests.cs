using Purch.Application.Promotions;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.UnitTests.Promotions;

public sealed class ItemPromoPricingCalculatorTests
{
    private static readonly Guid ItemA = Guid.Parse("00000000-0000-0000-0000-0000000000A1");
    private static readonly Guid ItemB = Guid.Parse("00000000-0000-0000-0000-0000000000B1");

    private static readonly Guid Line1 = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Line2 = Guid.Parse("00000000-0000-0000-0000-000000000002");

    [Fact]
    public void Same_item_bogo_spans_multiple_cart_lines_of_the_same_item()
    {
        // Buy 1 Take 1 on Item A. Two separate lines of A (e.g. added at different
        // times) total 5 units -> group size is 1 trigger + 1 free = 2 -> 2 groups
        // -> 2 free units, taken from the earliest line(s) first.
        var lines = new List<ItemPromoPricingCalculator.LineInput>
        {
            new(Line1, ItemA, Quantity: 3, UnitPrice: 10m),
            new(Line2, ItemA, Quantity: 2, UnitPrice: 10m),
        };
        var bogo = new BogoPromoRule { Name = "B1T1", TriggerItemId = ItemA, TriggerQuantity = 1, FreeItemId = ItemA, FreeQuantity = 1 };

        var results = ItemPromoPricingCalculator.Calculate(lines, [bogo], [], []);

        Assert.Equal(20m, results.Sum(r => r.DiscountAmount));
        // Earliest line (Line1) absorbs the free units first: 2 free units @ 10 = 20.
        var line1Result = results.Single(r => r.LineId == Line1);
        Assert.Equal(20m, line1Result.DiscountAmount);
        Assert.Equal("B1T1", line1Result.Label);
        var line2Result = results.Single(r => r.LineId == Line2);
        Assert.Equal(0m, line2Result.DiscountAmount);
    }

    [Fact]
    public void Different_item_bogo_is_capped_by_the_free_items_available_quantity()
    {
        // Buy 2 of A, get 1 of B free. 10 units of A -> 5 groups -> wants 5 free B,
        // but the cart only has 3 units of B in it, so only 3 can be freed.
        var lines = new List<ItemPromoPricingCalculator.LineInput>
        {
            new(Line1, ItemA, Quantity: 10, UnitPrice: 5m),
            new(Line2, ItemB, Quantity: 3, UnitPrice: 8m),
        };
        var bogo = new BogoPromoRule { Name = "A2 get B1", TriggerItemId = ItemA, TriggerQuantity = 2, FreeItemId = ItemB, FreeQuantity = 1 };

        var results = ItemPromoPricingCalculator.Calculate(lines, [bogo], [], []);

        var bLine = results.Single(r => r.LineId == Line2);
        Assert.Equal(24m, bLine.DiscountAmount); // 3 units * 8
        Assert.Equal("A2 get B1", bLine.Label);
        Assert.Equal(0m, results.Single(r => r.LineId == Line1).DiscountAmount);
    }

    [Fact]
    public void Combo_leaves_unpaired_units_at_full_price()
    {
        // 3 of A and 5 of B -> only 3 pairs are formed, 2 units of B stay unpaired.
        var lines = new List<ItemPromoPricingCalculator.LineInput>
        {
            new(Line1, ItemA, Quantity: 3, UnitPrice: 50m),
            new(Line2, ItemB, Quantity: 5, UnitPrice: 40m),
        };
        var combo = new ComboPromoRule { Name = "Combo", ItemAId = ItemA, ItemBId = ItemB, ComboPrice = 85m };

        var results = ItemPromoPricingCalculator.Calculate(lines, [], [combo], []);

        // Normal total for 3 pairs: 3 * (50 + 40) = 270. Combo total: 3 * 85 = 255.
        // Discount = 15, distributed proportionally (A contributes 150/270, B 120/270).
        var totalDiscount = results.Sum(r => r.DiscountAmount);
        Assert.Equal(15m, totalDiscount);

        var aResult = results.Single(r => r.LineId == Line1);
        var bResult = results.Single(r => r.LineId == Line2);
        Assert.Equal(Math.Round(15m * (150m / 270m), 2), aResult.DiscountAmount);
        Assert.Equal(Math.Round(15m * (120m / 270m), 2), bResult.DiscountAmount);
        Assert.Equal("COMBO ₱85.00", aResult.Label);

        // The 2 leftover, unpaired units of B are not part of any discounted quantity:
        // the discount attributed to B's line must be far less than 5 full units' worth.
        Assert.True(bResult.DiscountAmount < 5 * 40m);
    }

    [Theory]
    [InlineData(PromoDiscountType.Percentage, 20, 100, 20)]
    [InlineData(PromoDiscountType.FixedAmount, 15, 100, 15)]
    [InlineData(PromoDiscountType.FixedPrice, 70, 100, 30)]
    public void Item_discount_pass_handles_every_discount_type(PromoDiscountType type, decimal value, decimal unitPrice, decimal expectedPerUnitReduction)
    {
        var lines = new List<ItemPromoPricingCalculator.LineInput>
        {
            new(Line1, ItemA, Quantity: 2, UnitPrice: unitPrice),
        };
        var rule = new ItemDiscountPromoRule { Name = "Sale", ItemId = ItemA, DiscountType = type, DiscountValue = value };

        var results = ItemPromoPricingCalculator.Calculate(lines, [], [], [rule]);

        var result = results.Single();
        Assert.Equal(expectedPerUnitReduction * 2, result.DiscountAmount);
    }

    [Fact]
    public void Claimed_quantity_guard_prevents_double_discounting_across_rule_types()
    {
        // Item A is both a BOGO free-item and the target of an active item discount.
        // Buy 1 of the trigger item, take 1 of A free -> the freed unit of A is fully
        // claimed by BOGO, so the item-discount pass must not also discount it.
        var triggerItem = Guid.Parse("00000000-0000-0000-0000-0000000000C1");
        var triggerLine = Guid.Parse("00000000-0000-0000-0000-000000000003");

        var lines = new List<ItemPromoPricingCalculator.LineInput>
        {
            new(triggerLine, triggerItem, Quantity: 1, UnitPrice: 20m),
            new(Line1, ItemA, Quantity: 1, UnitPrice: 30m),
        };
        var bogo = new BogoPromoRule { Name = "B1T1", TriggerItemId = triggerItem, TriggerQuantity = 1, FreeItemId = ItemA, FreeQuantity = 1 };
        var itemDiscount = new ItemDiscountPromoRule { Name = "20% off A", ItemId = ItemA, DiscountType = PromoDiscountType.Percentage, DiscountValue = 20 };

        var results = ItemPromoPricingCalculator.Calculate(lines, [bogo], [], [itemDiscount]);

        var aResult = results.Single(r => r.LineId == Line1);

        // BOGO already claimed the single unit of A (full price, 30) — the item
        // discount pass sees zero unclaimed quantity and must add nothing more.
        // Without the guard this would double up to 30 + 6 = 36.
        Assert.Equal(30m, aResult.DiscountAmount);
        Assert.Equal("B1T1", aResult.Label);
    }

    [Fact]
    public void No_active_rules_yields_no_discounts()
    {
        var lines = new List<ItemPromoPricingCalculator.LineInput>
        {
            new(Line1, ItemA, Quantity: 4, UnitPrice: 25m),
        };

        var results = ItemPromoPricingCalculator.Calculate(lines, [], [], []);

        Assert.All(results, r => Assert.Equal(0m, r.DiscountAmount));
        Assert.All(results, r => Assert.Null(r.Label));
    }
}
