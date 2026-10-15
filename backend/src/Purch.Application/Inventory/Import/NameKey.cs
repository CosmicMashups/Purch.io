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
    public static int Distance(string a, string b) => BoundedDistance(a, b, Math.Max(a.Length, b.Length));

    /// <summary>Levenshtein distance, or <paramref name="max"/>+1 as soon as it is known to exceed <paramref name="max"/>.
    /// Only the diagonal band of width 2*max+1 is computed; small inputs use stack memory (no allocation).</summary>
    public static int BoundedDistance(string a, string b, int max)
    {
        var inf = max + 1;
        if (Math.Abs(a.Length - b.Length) > max) return inf;
        var n = b.Length + 1;
        Span<int> prev = n <= 256 ? stackalloc int[n] : new int[n];
        Span<int> cur = n <= 256 ? stackalloc int[n] : new int[n];
        for (var j = 0; j < n; j++) prev[j] = j <= max ? j : inf;
        for (var i = 1; i <= a.Length; i++)
        {
            var lo = Math.Max(1, i - max);
            var hi = Math.Min(b.Length, i + max);
            cur[lo - 1] = lo == 1 && i <= max ? i : inf;
            var rowMin = cur[lo - 1];
            for (var j = lo; j <= hi; j++)
            {
                var up = j <= i - 1 + max ? prev[j] + 1 : inf;
                var v = Math.Min(Math.Min(cur[j - 1] + 1, up), prev[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
                cur[j] = Math.Min(v, inf);
                rowMin = Math.Min(rowMin, cur[j]);
            }

            if (hi < b.Length) cur[hi + 1] = inf;
            if (rowMin > max) return inf;
            var t = prev;
            prev = cur;
            cur = t;
        }

        return Math.Min(prev[b.Length], inf);
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
    /// <summary>A key emits ~1 + L + L(L-1)/2 deletes, so only keys up to this length go in the delete index
    /// (24 chars ~ 300 strings each). Longer keys use a pigeonhole segment index instead: each is cut into
    /// MaxNearDistance+1 disjoint segments (3 entries per key); within distance 2 at least one segment survives
    /// unedited and appears in the query shifted by at most 2. Candidates are then verified with the bounded distance.</summary>
    public const int MaxIndexedKeyLength = 24;

    private readonly List<(Guid Id, string Name, string Key)> entries = [];
    private readonly Dictionary<string, List<int>> deletes = new(StringComparer.Ordinal);
    private const int Segments = NameKey.MaxNearDistance + 1;
    private readonly Dictionary<(int Length, int Segment, string Text), List<int>> longSegments = [];

    public NearMatchIndex(IEnumerable<(Guid Id, string Name)> candidates)
    {
        foreach (var (id, name) in candidates)
        {
            var key = NameKey.Of(name);
            if (key.Length == 0) continue;
            entries.Add((id, name, key));
            var i = entries.Count - 1;
            if (key.Length > MaxIndexedKeyLength)
            {
                for (var seg = 0; seg < Segments; seg++)
                {
                    var (start, len) = Segment(key.Length, seg);
                    var k = (key.Length, seg, key.Substring(start, len));
                    if (!longSegments.TryGetValue(k, out var bucket)) longSegments[k] = bucket = [];
                    bucket.Add(i);
                }

                continue;
            }

            foreach (var d in Deletes(key))
            {
                if (!deletes.TryGetValue(d, out var list)) deletes[d] = list = [];
                list.Add(i);
            }
        }
    }

    public IReadOnlyList<NearMatch> Find(string key, int take = 5)
    {
        if (key.Length == 0) return [];
        var found = new List<NearMatch>();
        void Check(int i)
        {
            var e = entries[i];
            if (NameKey.IsNear(key, e.Key, out var dist)) found.Add(new NearMatch(e.Id, e.Name, dist));
        }

        // Indexed (short) entries can only be near when the query is at most MaxIndexedKeyLength + 2 long.
        if (key.Length <= MaxIndexedKeyLength + NameKey.MaxNearDistance)
        {
            var seen = new HashSet<int>();
            foreach (var d in Deletes(key))
            {
                if (!deletes.TryGetValue(d, out var list)) continue;
                foreach (var i in list)
                {
                    if (seen.Add(i)) Check(i);
                }
            }
        }

        if (key.Length + NameKey.MaxNearDistance > MaxIndexedKeyLength)
        {
            var seenLong = new HashSet<int>();
            for (var len = Math.Max(MaxIndexedKeyLength + 1, key.Length - NameKey.MaxNearDistance); len <= key.Length + NameKey.MaxNearDistance; len++)
            {
                for (var seg = 0; seg < Segments; seg++)
                {
                    var (start, segLen) = Segment(len, seg);
                    for (var shift = -NameKey.MaxNearDistance; shift <= NameKey.MaxNearDistance; shift++)
                    {
                        var at = start + shift;
                        if (at < 0 || at + segLen > key.Length) continue;
                        if (!longSegments.TryGetValue((len, seg, key.Substring(at, segLen)), out var bucket)) continue;
                        foreach (var i in bucket)
                        {
                            if (seenLong.Add(i)) Check(i);
                        }
                    }
                }
            }
        }

        return found.OrderBy(m => m.Distance).ThenBy(m => m.Name, StringComparer.Ordinal).Take(take).ToList();
    }

    private static (int Start, int Length) Segment(int keyLength, int seg)
    {
        var start = seg * keyLength / Segments;
        return (start, ((seg + 1) * keyLength / Segments) - start);
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
