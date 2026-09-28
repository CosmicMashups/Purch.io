using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Purch.Application.Promotions;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.UnitTests.Promotions;

/// <summary>
/// Runs the item-promo part of shared/pricing-scenarios.json — the same cases the web's TypeScript engine and
/// the Flutter Dart engine run — against the backend calculator, so the three copies of the rules cannot
/// drift apart unnoticed. Cart-level rules (Senior/PWD, promo codes) live in TransactionService, not in the
/// calculator, so scenarios that use them are covered by the client engines and the service's own tests.
/// </summary>
public sealed class SharedPricingScenariosTests
{
    private static readonly JsonElement Root = Load();

    public static TheoryData<string> ItemPromoScenarios()
    {
        var data = new TheoryData<string>();
        foreach (var scenario in Root.GetProperty("scenarios").EnumerateArray())
        {
            if (IsItemPromoOnly(scenario))
            {
                data.Add(scenario.GetProperty("name").GetString()!);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ItemPromoScenarios))]
    public void Item_promos_match_the_shared_scenario(string name)
    {
        var scenario = Root.GetProperty("scenarios").EnumerateArray().Single(s => s.GetProperty("name").GetString() == name);
        var now = DateTimeOffset.Parse(scenario.TryGetProperty("now", out var n) ? n.GetString()! : Root.GetProperty("now").GetString()!, CultureInfo.InvariantCulture);

        // Lines keep their order: the calculator orders by LineId, so ids follow the position in the cart.
        var lineIds = new Dictionary<string, Guid>();
        var lines = new List<ItemPromoPricingCalculator.LineInput>();
        var index = 0;
        foreach (var line in scenario.GetProperty("lines").EnumerateArray())
        {
            var lineId = new Guid(++index, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0]);
            lineIds[line.GetProperty("lineId").GetString()!] = lineId;
            lines.Add(new(lineId, ItemId(line.GetProperty("itemId").GetString()!), line.GetProperty("quantity").GetDecimal(), line.GetProperty("unitPrice").GetDecimal()));
        }

        var bogo = new List<BogoPromoRule>();
        var combo = new List<ComboPromoRule>();
        var itemDiscount = new List<ItemDiscountPromoRule>();
        if (scenario.TryGetProperty("rules", out var rules))
        {
            foreach (var r in Rules(rules, "bogo", now))
            {
                bogo.Add(new()
                {
                    Name = r.GetProperty("name").GetString()!,
                    TriggerItemId = ItemId(r.GetProperty("triggerItemId").GetString()!),
                    TriggerQuantity = r.GetProperty("triggerQuantity").GetInt32(),
                    FreeItemId = ItemId(r.GetProperty("freeItemId").GetString()!),
                    FreeQuantity = r.GetProperty("freeQuantity").GetInt32(),
                });
            }

            foreach (var r in Rules(rules, "combo", now))
            {
                combo.Add(new()
                {
                    ItemAId = ItemId(r.GetProperty("itemAId").GetString()!),
                    ItemBId = ItemId(r.GetProperty("itemBId").GetString()!),
                    ComboPrice = r.GetProperty("comboPrice").GetDecimal(),
                });
            }

            foreach (var r in Rules(rules, "itemDiscount", now))
            {
                itemDiscount.Add(new()
                {
                    ItemId = ItemId(r.GetProperty("itemId").GetString()!),
                    DiscountType = Enum.Parse<PromoDiscountType>(r.GetProperty("discountType").GetString()!, ignoreCase: true),
                    DiscountValue = r.GetProperty("discountValue").GetDecimal(),
                });
            }
        }

        var results = ItemPromoPricingCalculator.Calculate(lines, bogo, combo, itemDiscount);

        var expect = scenario.GetProperty("expect");
        if (expect.TryGetProperty("itemPromoDiscountAmount", out var total))
        {
            Assert.Equal((double)total.GetDecimal(), (double)results.Sum(r => r.DiscountAmount), 0.02);
        }

        if (expect.TryGetProperty("lineDiscounts", out var perLine))
        {
            foreach (var wanted in perLine.EnumerateObject())
            {
                var result = results.Single(r => r.LineId == lineIds[wanted.Name]);
                if (wanted.Value.TryGetProperty("discount", out var discount))
                {
                    Assert.Equal((double)discount.GetDecimal(), (double)result.DiscountAmount, 0.02);
                }

                if (wanted.Value.TryGetProperty("label", out var label))
                {
                    Assert.Equal(label.ValueKind == JsonValueKind.Null ? null : label.GetString(), result.Label);
                }
            }
        }
    }

    /// <summary>True when the scenario asserts only what the calculator itself produces.</summary>
    private static bool IsItemPromoOnly(JsonElement scenario)
    {
        if (scenario.TryGetProperty("seniorPwdApplied", out var senior) && senior.GetBoolean())
        {
            return false;
        }

        if (scenario.TryGetProperty("promoCode", out _))
        {
            return false;
        }

        var expect = scenario.GetProperty("expect");
        return expect.TryGetProperty("itemPromoDiscountAmount", out _) || expect.TryGetProperty("lineDiscounts", out _);
    }

    /// <summary>The rules that are active at <paramref name="now"/>, as the repositories return them.</summary>
    private static IEnumerable<JsonElement> Rules(JsonElement rules, string kind, DateTimeOffset now)
    {
        if (!rules.TryGetProperty(kind, out var list))
        {
            yield break;
        }

        foreach (var rule in list.EnumerateArray())
        {
            if (rule.TryGetProperty("isActive", out var active) && !active.GetBoolean())
            {
                continue;
            }

            if (rule.TryGetProperty("startsAt", out var starts) && starts.ValueKind == JsonValueKind.String && DateTimeOffset.Parse(starts.GetString()!, CultureInfo.InvariantCulture) > now)
            {
                continue;
            }

            if (rule.TryGetProperty("endsAt", out var ends) && ends.ValueKind == JsonValueKind.String && DateTimeOffset.Parse(ends.GetString()!, CultureInfo.InvariantCulture) < now)
            {
                continue;
            }

            yield return rule;
        }
    }

    /// <summary>A stable Guid per scenario item name ("item-a" is always the same Guid).</summary>
    private static Guid ItemId(string name) => new(SHA256.HashData(Encoding.UTF8.GetBytes(name))[..16]);

    private static JsonElement Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "shared", "pricing-scenarios.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.Clone();
    }
}
