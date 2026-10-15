using System.Globalization;
using System.Text;

namespace Purch.Application.Inventory.Import;

/// <summary>Comparison key for item/category names: trim, collapse whitespace, invariant case-fold, strip diacritics,
/// <c>&amp;</c> = "and", strip punctuation/symbols. "Café  &amp; Co" and "cafe and co" share a key.</summary>
public static class NameKey
{
    public static string Of(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;

        var decomposed = s.Replace("&", " and ", StringComparison.Ordinal).Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        var pendingSpace = false;
        foreach (var ch in decomposed)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsWhiteSpace(ch))
            {
                pendingSpace = sb.Length > 0;
                continue;
            }

            if (char.IsPunctuation(ch) || char.IsSymbol(ch)) continue;
            if (pendingSpace) _ = sb.Append(' ');
            pendingSpace = false;
            _ = sb.Append(char.ToLowerInvariant(ch));
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>Levenshtein distance between two keys.</summary>
    public static int Distance(string a, string b)
    {
        var prev = new int[b.Length + 1];
        var cur = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) prev[j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            cur[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                cur[j] = Math.Min(Math.Min(cur[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
            }

            (prev, cur) = (cur, prev);
        }

        return prev[b.Length];
    }
}
