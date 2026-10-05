using System.Text.Json;
using Purch.Domain.Entities;

namespace Purch.Application.Catalog;

/// <summary>Reads and writes a combo slot's per-choice surcharges, which are stored as a JSON object of
/// item id to extra price. Shared by the slot editor and by checkout so both agree on the numbers.</summary>
public static class ComboChoiceUpcharges
{
    public static IReadOnlyDictionary<Guid, decimal> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<Guid, decimal>();
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<Guid, decimal>>(json) ?? [];
        }
        catch (JsonException)
        {
            // A damaged value must never block a sale; it simply means no choice carries a surcharge.
            return new Dictionary<Guid, decimal>();
        }
    }

    /// <summary>Null when nothing costs extra, so a slot without surcharges stores nothing.</summary>
    public static string? Serialize(IEnumerable<ComboChoiceUpchargeDto> entries)
    {
        var map = entries.Where(entry => entry.Amount > 0).ToDictionary(entry => entry.ItemId, entry => entry.Amount);
        return map.Count == 0 ? null : JsonSerializer.Serialize(map);
    }

    public static IReadOnlyList<ComboChoiceUpchargeDto> ToDtos(string? json)
    {
        return [.. Parse(json).Select(pair => new ComboChoiceUpchargeDto(pair.Key, pair.Value))];
    }

    /// <summary>The extra price for choosing this item in this slot; zero when it is included.</summary>
    public static decimal For(ItemComboComponent slot, Guid itemId)
    {
        return Parse(slot.ChoiceUpchargesJson).GetValueOrDefault(itemId);
    }
}
