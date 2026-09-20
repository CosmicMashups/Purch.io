using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.Promotions;

/// <summary>Per-line pricing result of an automatic item-level promo (BOGO/combo/item
/// discount) — an amount to subtract from that line's total, plus the label to show
/// on the cart/receipt.</summary>
public sealed record ItemPromoLineResult(Guid LineId, decimal DiscountAmount, string? Label);

/// <summary>
/// Prices the automatic, no-code-entry item promos (BOGO, combo bundle, item
/// discount) against a cart's lines. Pulled out of TransactionService so the
/// core algorithm can be unit tested without standing up the whole POS engine.
///
/// Passes run in a fixed order — BOGO, then Combo, then ItemDiscount — and each
/// pass tracks how much of each line's quantity it has already "claimed" (i.e.
/// already discounted). Later passes, and later rules within the same pass, may
/// only discount quantity that is still unclaimed on a line. This is what stops
/// the same physical unit from being discounted twice when it happens to match
/// more than one active rule (e.g. an item that is both a BOGO free-item and the
/// target of an active item discount) — without it, stacking rules could give
/// away more discount than any single promo actually promises.
/// </summary>
public static class ItemPromoPricingCalculator
{
    public sealed record LineInput(Guid LineId, Guid ItemId, decimal Quantity, decimal UnitPrice);

    public static IReadOnlyList<ItemPromoLineResult> Calculate(
        IReadOnlyList<LineInput> lines,
        IReadOnlyList<BogoPromoRule> bogoRules,
        IReadOnlyList<ComboPromoRule> comboRules,
        IReadOnlyList<ItemDiscountPromoRule> itemDiscountRules)
    {
        // Quantity of each line already claimed by an earlier pass/rule.
        var claimed = lines.ToDictionary(line => line.LineId, _ => 0m);
        var discount = lines.ToDictionary(line => line.LineId, _ => 0m);
        var label = lines.ToDictionary(line => line.LineId, string? (_) => null);

        decimal Unclaimed(LineInput line)
        {
            return line.Quantity - claimed[line.LineId];
        }

        // --- BOGO pass ---
        foreach (var rule in bogoRules)
        {
            var triggerLines = lines.Where(l => l.ItemId == rule.TriggerItemId).OrderBy(l => l.LineId).ToList();
            var triggerQty = triggerLines.Sum(l => l.Quantity);

            int groups;
            decimal freeUnits;

            if (rule.TriggerItemId == rule.FreeItemId)
            {
                // The same units can't be both "trigger" and "free" — a group consumes
                // TriggerQuantity + FreeQuantity units total.
                var groupSize = rule.TriggerQuantity + rule.FreeQuantity;
                groups = groupSize > 0 ? (int)Math.Floor(triggerQty / groupSize) : 0;
                freeUnits = groups * rule.FreeQuantity;
            }
            else
            {
                groups = rule.TriggerQuantity > 0 ? (int)Math.Floor(triggerQty / rule.TriggerQuantity) : 0;
                var freeLinesAvailable = lines.Where(l => l.ItemId == rule.FreeItemId).Sum(Unclaimed);
                freeUnits = Math.Min(groups * rule.FreeQuantity, freeLinesAvailable);
            }

            if (freeUnits <= 0)
            {
                continue;
            }

            var freeLines = lines.Where(l => l.ItemId == rule.FreeItemId).OrderBy(l => l.LineId).ToList();
            var remaining = freeUnits;
            foreach (var freeLine in freeLines)
            {
                if (remaining <= 0)
                {
                    break;
                }

                var take = Math.Min(remaining, Unclaimed(freeLine));
                if (take <= 0)
                {
                    continue;
                }

                claimed[freeLine.LineId] += take;
                discount[freeLine.LineId] += take * freeLine.UnitPrice;
                label[freeLine.LineId] = string.IsNullOrWhiteSpace(rule.Name) ? "BUY 1 TAKE 1" : rule.Name;
                remaining -= take;
            }
        }

        // --- Combo pass ---
        foreach (var rule in comboRules)
        {
            var aLines = lines.Where(l => l.ItemId == rule.ItemAId).OrderBy(l => l.LineId).ToList();
            var bLines = lines.Where(l => l.ItemId == rule.ItemBId).OrderBy(l => l.LineId).ToList();

            var aAvailable = aLines.Sum(Unclaimed);
            var bAvailable = bLines.Sum(Unclaimed);
            var pairs = Math.Min(aAvailable, bAvailable);

            if (pairs <= 0)
            {
                continue;
            }

            var unitPriceA = aLines.Count > 0 ? aLines[0].UnitPrice : 0m;
            var unitPriceB = bLines.Count > 0 ? bLines[0].UnitPrice : 0m;

            var normalTotal = pairs * (unitPriceA + unitPriceB);
            var comboTotal = pairs * rule.ComboPrice;
            var totalDiscount = Math.Max(normalTotal - comboTotal, 0m);

            if (totalDiscount <= 0)
            {
                // Still claim the paired quantity — the combo matched, it just doesn't
                // reduce price (e.g. an admin set ComboPrice at or above normal price).
                ClaimAcrossLines(aLines, pairs, claimed, discount: null, Unclaimed);
                ClaimAcrossLines(bLines, pairs, claimed, discount: null, Unclaimed);
                continue;
            }

            // Distribute the discount across the two items proportionally to each
            // item's contribution to the combined normal price.
            var discountA = normalTotal > 0 ? totalDiscount * pairs * unitPriceA / normalTotal : totalDiscount / 2;
            var discountB = totalDiscount - discountA;

            var comboLabel = $"COMBO ₱{rule.ComboPrice:F2}";
            ClaimAcrossLines(aLines, pairs, claimed, discountA, Unclaimed, discount, label, comboLabel);
            ClaimAcrossLines(bLines, pairs, claimed, discountB, Unclaimed, discount, label, comboLabel);
        }

        // --- Item discount pass ---
        foreach (var rule in itemDiscountRules)
        {
            var matchingLines = lines.Where(l => l.ItemId == rule.ItemId);
            foreach (var line in matchingLines)
            {
                var unclaimedQty = Unclaimed(line);
                if (unclaimedQty <= 0)
                {
                    continue;
                }

                var reduction = rule.DiscountType switch
                {
                    PromoDiscountType.Percentage => line.UnitPrice * rule.DiscountValue / 100m,
                    PromoDiscountType.FixedAmount => Math.Min(rule.DiscountValue, line.UnitPrice),
                    PromoDiscountType.FixedPrice => Math.Max(line.UnitPrice - rule.DiscountValue, 0m),
                    _ => 0m,
                };

                if (reduction <= 0)
                {
                    continue;
                }

                claimed[line.LineId] += unclaimedQty;
                discount[line.LineId] += reduction * unclaimedQty;
                label[line.LineId] = rule.DiscountType switch
                {
                    PromoDiscountType.Percentage => $"{rule.DiscountValue}% OFF",
                    PromoDiscountType.FixedAmount => $"₱{rule.DiscountValue:F2} OFF",
                    PromoDiscountType.FixedPrice => "SALE PRICE",
                    _ => label[line.LineId],
                };
            }
        }

        return [.. lines.Select(line => new ItemPromoLineResult(line.LineId, Math.Round(discount[line.LineId], 2), label[line.LineId]))];
    }

    /// <summary>Distributes a claimed quantity (and, optionally, a discount amount)
    /// across a set of lines for the same item, earliest line first, up to each
    /// line's unclaimed quantity.</summary>
    private static void ClaimAcrossLines(
        List<LineInput> orderedLines,
        decimal quantityToClaim,
        Dictionary<Guid, decimal> claimed,
        decimal? discount,
        Func<LineInput, decimal> unclaimed,
        Dictionary<Guid, decimal>? discountSink = null,
        Dictionary<Guid, string?>? labelSink = null,
        string? label = null)
    {
        var remainingQty = quantityToClaim;

        for (var i = 0; i < orderedLines.Count && remainingQty > 0; i++)
        {
            var line = orderedLines[i];
            var lineUnclaimed = unclaimed(line);
            var take = Math.Min(remainingQty, lineUnclaimed);
            if (take <= 0)
            {
                continue;
            }

            claimed[line.LineId] += take;

            if (discount is not null && discountSink is not null)
            {
                // Proportional share of the total discount for this slice of the claimed quantity.
                var share = quantityToClaim > 0 ? discount.Value * (take / quantityToClaim) : 0m;
                discountSink[line.LineId] += share;

                if (labelSink is not null)
                {
                    labelSink[line.LineId] = label;
                }
            }

            remainingQty -= take;
        }
    }
}
