using System.Globalization;
using System.Text;

namespace Purch.Application.Inventory.Import;

/// <summary>Comparison key for item/category names: trim, collapse whitespace, invariant case-fold, strip diacritics
/// on Latin/Greek/Cyrillic letters (other scripts keep their marks), <c>&amp;</c> = "and", hyphen/slash = space,
/// strip other punctuation/symbols. "Café  &amp; Co" and "cafe and co" share a key; "Coca-Cola" == "Coca Cola".</summary>
public static class NameKey
{
    /// <summary>Largest edit distance that counts as a "possible duplicate".</summary>
    public const int MaxNearDistance = 2;

    /// <summary>Distance 1 is only a near match when both keys are at least this long ("tea"/"tee" are different).</summary>
    public const int MinLengthForDistance1 = 4;

    /// <summary>Distance 2 is only a near match when both keys are at least this long.</summary>
    public const int MinLengthForDistance2 = 6;

    public static string Of(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;

        var decomposed = s.Replace("&", " and ", StringComparison.Ordinal).Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        var pendingSpace = false;
        var lastBase = '\0';
        foreach (var ch in decomposed)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat == UnicodeCategory.NonSpacingMark)
            {
                if (IsLatinGreekCyrillic(lastBase)) continue;
                _ = sb.Append(ch);
                continue;
            }

            if (char.IsWhiteSpace(ch) || ch == '/' || cat == UnicodeCategory.DashPunctuation)
            {
                pendingSpace = sb.Length > 0;
                continue;
            }

            if (char.IsPunctuation(ch) || char.IsSymbol(ch)) continue;
            if (pendingSpace) _ = sb.Append(' ');
            pendingSpace = false;
            lastBase = ch;
            _ = sb.Append(char.ToLowerInvariant(ch));
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static bool IsLatinGreekCyrillic(char c) =>
        c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= 'À' and <= 'ɏ') or (>= 'Ḁ' and <= 'ỿ')
            or (>= 'Ͱ' and <= 'Ͽ') or (>= 'ἀ' and <= '῿') or (>= 'Ѐ' and <= 'ԯ');

    /// <summary>Levenshtein distance between two keys.</summary>
    public static int Distance(string a, string b) => BoundedDistance(a, b, int.MaxValue - 1);

    /// <summary>Levenshtein distance, or <paramref name="max"/>+1 as soon as it is known to exceed <paramref name="max"/>.</summary>
    public static int BoundedDistance(string a, string b, int max)
    {
        if (Math.Abs(a.Length - b.Length) > max) return max + 1;
        var prev = new int[b.Length + 1];
        var cur = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) prev[j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            cur[0] = i;
            var rowMin = cur[0];
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                cur[j] = Math.Min(Math.Min(cur[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
                rowMin = Math.Min(rowMin, cur[j]);
            }

            if (rowMin > max) return max + 1;
            (prev, cur) = (cur, prev);
        }

        return Math.Min(prev[b.Length], max + 1);
    }

    /// <summary>The "possible duplicate" rule: distance &lt;= 2 subject to the length thresholds above; keys that
    /// differ only in digits ("coke 1l"/"coke 2l") are distinct; an empty key never matches.</summary>
    public static bool IsNear(string a, string b, out int distance)
    {
        distance = int.MaxValue;
        if (a.Length == 0 || b.Length == 0) return false;
        distance = BoundedDistance(a, b, MaxNearDistance);
        return distance switch
        {
            0 => true,
            > MaxNearDistance => false,
            _ when StripDigits(a) == StripDigits(b) => false,
            1 => Math.Min(a.Length, b.Length) >= MinLengthForDistance1,
            _ => Math.Min(a.Length, b.Length) >= MinLengthForDistance2,
        };
    }

    private static string StripDigits(string s) => string.Concat(s.Where(c => !char.IsDigit(c)));
}

/// <summary>Finds near matches (<see cref="NameKey.IsNear"/>) without comparing against every candidate: a
/// symmetric-delete index (all keys with up to 2 chars deleted) yields candidates, which are then verified.</summary>
public sealed class NearMatchIndex
{
    private readonly List<(Guid Id, string Name, string Key)> entries = [];
    private readonly Dictionary<string, List<int>> deletes = new(StringComparer.Ordinal);

    public NearMatchIndex(IEnumerable<(Guid Id, string Name)> candidates)
    {
        foreach (var (id, name) in candidates)
        {
            var key = NameKey.Of(name);
            if (key.Length == 0) continue;
            entries.Add((id, name, key));
            foreach (var d in Deletes(key))
            {
                if (!deletes.TryGetValue(d, out var list)) deletes[d] = list = [];
                list.Add(entries.Count - 1);
            }
        }
    }

    public IReadOnlyList<NearMatch> Find(string key, int take = 5)
    {
        if (key.Length == 0) return [];
        var seen = new HashSet<int>();
        var found = new List<NearMatch>();
        foreach (var d in Deletes(key))
        {
            if (!deletes.TryGetValue(d, out var list)) continue;
            foreach (var i in list)
            {
                if (!seen.Add(i)) continue;
                var e = entries[i];
                if (NameKey.IsNear(key, e.Key, out var dist)) found.Add(new NearMatch(e.Id, e.Name, dist));
            }
        }

        return found.OrderBy(m => m.Distance).ThenBy(m => m.Name, StringComparer.Ordinal).Take(take).ToList();
    }

    private static HashSet<string> Deletes(string key)
    {
        var result = new HashSet<string>(StringComparer.Ordinal) { key };
        var frontier = new List<string> { key };
        for (var depth = 0; depth < NameKey.MaxNearDistance; depth++)
        {
            var next = new List<string>();
            foreach (var s in frontier)
            {
                for (var i = 0; i < s.Length; i++)
                {
                    var d = s.Remove(i, 1);
                    if (result.Add(d)) next.Add(d);
                }
            }

            frontier = next;
        }

        return result;
    }
}
