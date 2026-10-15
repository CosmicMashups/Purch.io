using Purch.Application.Inventory.Import;

namespace Purch.UnitTests.Inventory.Import;

public sealed class NameKeyTests
{
    [Fact]
    public void Cafe_and_co_variants_share_a_key() =>
        Assert.Equal(NameKey.Of("cafe and co"), NameKey.Of("  Café  & Co "));

    [Fact]
    public void Punctuation_is_stripped_and_spaces_collapsed() =>
        Assert.Equal("ice cream", NameKey.Of("  Ice   Cream! "));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_is_empty(string? s) => Assert.Equal("", NameKey.Of(s));

    [Fact]
    public void Edit_distance_counts_single_edits() =>
        Assert.Equal(1, NameKey.Distance("bevrages", "beverages"));

    [Fact]
    public void Hyphen_and_slash_are_spaces()
    {
        Assert.Equal(NameKey.Of("Coca Cola"), NameKey.Of("Coca-Cola"));
        Assert.Equal("salt pepper", NameKey.Of("Salt/Pepper"));
    }

    [Fact]
    public void Non_latin_marks_are_kept() =>
        Assert.NotEqual(NameKey.Of("か"), NameKey.Of("が")); // ka vs ga

    [Fact]
    public void Greek_accents_are_stripped() =>
        Assert.Equal(NameKey.Of("α"), NameKey.Of("ά")); // alpha vs alpha-tonos

    [Fact]
    public void Symbols_only_is_empty_and_never_near() =>
        Assert.False(NameKey.IsNear(NameKey.Of("!!!"), NameKey.Of("?"), out _));

    [Theory]
    [InlineData("Coke 1L", "Coke 2L", false)]
    [InlineData("Tea", "Tee", false)]
    [InlineData("Bevrages", "Beverages", true)]
    [InlineData("Juice", "Jucie", false)]
    [InlineData("Coffee", "Cofeee", true)]
    [InlineData("Banana", "Bnanaa", true)]
    [InlineData("Cola", "Cola", true)]
    public void Near_rule_thresholds(string a, string b, bool near) =>
        Assert.Equal(near, NameKey.IsNear(NameKey.Of(a), NameKey.Of(b), out _));

    [Fact]
    public void Index_matches_naive_scan()
    {
        var rnd = new Random(7);
        string Word() => new(Enumerable.Range(0, rnd.Next(3, 9)).Select(_ => (char)('a' + rnd.Next(4))).ToArray());
        var names = Enumerable.Range(0, 300).Select(_ => (Id: Guid.NewGuid(), Name: Word())).ToList();
        var index = new NearMatchIndex(names);
        for (var q = 0; q < 200; q++)
        {
            var key = NameKey.Of(Word());
            var naive = names.Where(n => NameKey.IsNear(key, NameKey.Of(n.Name), out _)).Select(n => n.Id).ToHashSet();
            var fast = index.Find(key, int.MaxValue).Select(m => m.Id).ToHashSet();
            Assert.True(naive.SetEquals(fast), $"mismatch for {key}");
        }
    }
}
