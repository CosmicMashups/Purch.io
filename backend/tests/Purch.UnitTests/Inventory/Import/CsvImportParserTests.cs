using System.Text;
using Purch.Application.Inventory.Import;
using Purch.Domain.Enums;

namespace Purch.UnitTests.Inventory.Import;

public sealed class CsvImportParserTests
{
    private const string CatalogHeader = "type,sku,barcode,name,category,price,cost,reorder_threshold,unit,active";

    private static ParsedImport Parse(string csv, ImportKind kind = ImportKind.Catalog, bool bom = false)
    {
        var bytes = Encoding.UTF8.GetBytes(csv);
        if (bom) bytes = [0xEF, 0xBB, 0xBF, .. bytes];
        return CsvImportParser.Parse(new MemoryStream(bytes), kind);
    }

    [Fact]
    public void Bom_is_tolerated()
    {
        var parsed = Parse(CatalogHeader + "\nitem,S1,,Cola,Drinks,10,,,,\n", bom: true);
        Assert.Equal("Cola", Assert.Single(parsed.Rows).Name);
    }

    [Fact]
    public void Blank_is_unchanged_clear_sets_flag_zero_stays_zero()
    {
        var parsed = Parse(CatalogHeader + "\nitem,S1,,A,,,,,,\nitem,S2,,B,,0, clear ,,,\nitem,S3,,C,,,0,,,\n");
        var a = parsed.Rows[0];
        Assert.Null(a.Price);
        Assert.Null(a.Cost);
        Assert.False(a.ClearCost);
        var b = parsed.Rows[1];
        Assert.Equal(0m, b.Price);
        Assert.True(b.ClearCost);
        Assert.Null(b.Cost);
        Assert.Equal(0m, parsed.Rows[2].Cost);
        Assert.False(parsed.Rows[2].ClearCost);
    }

    [Fact]
    public void Barcode_kept_as_text_and_scientific_notation_rejected()
    {
        var parsed = Parse(CatalogHeader + "\nitem,,0012345,A,,,,,,\nitem,,1.2E+5,B,,,,,,\nitem,,1.23457E+11,C,,,,,,\n");
        Assert.Equal("0012345", parsed.Rows[0].Barcode);
        Assert.Empty(parsed.Rows[0].Errors);
        Assert.Contains(parsed.Rows[1].Errors, e => e.Contains("barcode", StringComparison.OrdinalIgnoreCase));
        Assert.NotEmpty(parsed.Rows[2].Errors);
        Assert.Equal(3, parsed.Rows[1].Line);
    }

    [Fact]
    public void Rows_over_5000_are_rejected()
    {
        var sb = new StringBuilder(CatalogHeader).Append('\n');
        for (var i = 0; i < 5001; i++) sb.Append("item,S").Append(i).Append(",,N,,,,,,\n");
        _ = Assert.Throws<ImportFormatException>(() => Parse(sb.ToString()));
    }

    [Fact]
    public void Exactly_5000_rows_are_accepted()
    {
        var sb = new StringBuilder(CatalogHeader).Append('\n');
        for (var i = 0; i < 5000; i++) sb.Append("item,S").Append(i).Append(",,N,,,,,,\n");
        Assert.Equal(5000, Parse(sb.ToString()).Rows.Count);
    }

    [Fact]
    public void Over_5_MB_is_rejected()
    {
        var big = CatalogHeader + "\nitem,S1,,\"" + new string('x', 5 * 1024 * 1024) + "\",,,,,,\n";
        _ = Assert.Throws<ImportFormatException>(() => Parse(big));
    }

    [Theory]
    [InlineData("type,sku,barcode,name,bogus")]
    [InlineData("type,sku,sku,name")]
    [InlineData("type,barcode,price")]
    public void Bad_headers_are_rejected(string header) =>
        Assert.Throws<ImportFormatException>(() => Parse(header + "\n"));

    [Fact]
    public void Read_only_columns_are_accepted_and_ignored()
    {
        var parsed = Parse("id,type,sku,name,current_stock,updated_at\nabc,item,S1,A,5,2026-01-01\n");
        Assert.Equal("S1", Assert.Single(parsed.Rows).Sku);
    }

    [Fact]
    public void Leading_apostrophe_guard_is_stripped_once_only_before_formula_chars()
    {
        var parsed = Parse("type,sku,name\nitem,'=S1,'@Cola\nitem,''=X,'Plain\n");
        Assert.Equal("=S1", parsed.Rows[0].Sku);
        Assert.Equal("@Cola", parsed.Rows[0].Name);
        Assert.Equal("''=X", parsed.Rows[1].Sku);
        Assert.Equal("'Plain", parsed.Rows[1].Name);
    }

    [Fact]
    public void Quoted_fields_with_embedded_newlines_and_quotes()
    {
        var parsed = Parse("type,sku,name\nitem,S1,\"Big \"\"Cola\"\"\nLarge\"\nitem,S2,B\n");
        Assert.Equal("Big \"Cola\"\nLarge", parsed.Rows[0].Name);
        Assert.Equal(4, parsed.Rows[1].Line);
    }

    [Fact]
    public void Invalid_number_is_a_row_error_with_line()
    {
        var parsed = Parse(CatalogHeader + "\nitem,S1,,A,,1,5,,,\nitem,S2,,B,,abc,,,,\n");
        Assert.Empty(parsed.Rows[0].Errors);
        Assert.Contains(parsed.Rows[1].Errors, e => e.Contains("price"));
        Assert.Equal(3, parsed.Rows[1].Line);
    }

    [Fact]
    public void Stock_kind_reads_counted_qty_and_note()
    {
        var parsed = Parse("sku,barcode,counted_qty,note\nS1,,12.5,shelf\n", ImportKind.Stock);
        Assert.Equal(12.5m, parsed.Rows[0].CountedQty);
        Assert.Equal("shelf", parsed.Rows[0].Note);
    }

    [Theory]
    [InlineData("1.2E+5", true)]
    [InlineData("1E+10", true)]
    [InlineData("1.23457E11", true)]
    [InlineData("12E5", false)]
    [InlineData("1E10", false)]
    [InlineData("4E7", false)]
    public void Scientific_notation_rule_for_barcode_and_sku(string code, bool flagged)
    {
        var parsed = Parse($"type,sku,barcode,name\nitem,{code},,A\nitem,X,{code},B\n");
        Assert.Equal(flagged, parsed.Rows[0].Errors.Count > 0);
        Assert.Equal(flagged, parsed.Rows[1].Errors.Count > 0);
        if (!flagged)
        {
            Assert.Equal(code, parsed.Rows[0].Sku);
            Assert.Equal(code, parsed.Rows[1].Barcode);
        }
    }

    [Fact]
    public void Negative_numbers_are_row_errors()
    {
        var parsed = Parse(CatalogHeader + "\nitem,S1,,A,,-1,,,,\nitem,S2,,B,,,-2,,,\nitem,S3,,C,,,,-3,,\n");
        Assert.All(parsed.Rows, r => Assert.Single(r.Errors));
        var stock = Parse("sku,counted_qty\nS1,-1\n", ImportKind.Stock);
        Assert.Single(stock.Rows[0].Errors);
    }
}
