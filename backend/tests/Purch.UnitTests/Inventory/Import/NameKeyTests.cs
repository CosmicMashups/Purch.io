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
}
