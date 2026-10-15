using System.Text;
using Purch.Application.Inventory.Import;
using Purch.Domain.Enums;

namespace Purch.UnitTests.Inventory.Import;

public sealed class ImportPlannerTests
{
    private const string Header = "type,sku,barcode,name,category,price,cost,reorder_threshold,unit,active";

    private readonly List<ExistingRecord> records = [];
    private readonly List<ExistingCategory> categories = [];

    private static ParsedImport Parse(string body, ImportKind kind = ImportKind.Catalog, string header = Header) =>
        CsvImportParser.Parse(new MemoryStream(Encoding.UTF8.GetBytes(header + "\n" + body)), kind);

    private ImportPlan Plan(string body, ImportKind kind = ImportKind.Catalog, string header = Header,
        Dictionary<string, ImportResolution>? resolutions = null) =>
        ImportPlanner.Plan(Parse(body, kind, header), new ExistingCatalog(records, categories), resolutions);

    [Fact]
    public void Unchanged_matches_are_counted_not_listed()
    {
        _ = Rec("Cola", sku: "S1", price: 10m);
        var plan = Plan("item,S1,,Cola,,10,,,,\n");
        Assert.Empty(plan.WillUpdate);
        Assert.Equal(1, plan.UnchangedCount);
    }

    [Fact]
    public void Identical_name_with_new_sku_needs_decision()
    {
        _ = Rec("Cola", sku: "S1");
        var d = Assert.Single(Plan("item,S2,,COLA,,1,,,,\n").NeedsDecision);
        Assert.Equal("row:2", d.Key);
    }

    [Fact]
    public void Symbols_only_name_is_row_error() =>
        Assert.Single(Plan("item,S1,,!!!,,1,,,,\n").RowErrors);

    [Fact]
    public void Resolved_decision_no_longer_blocks()
    {
        var bev = Cat("Beverages");
        var key = Assert.Single(Plan("item,S1,,Cola,Bevrages,10,,,,\n").NeedsDecision).Key;
        Assert.Equal("cat:item:bevrages", key);

        var plan = Plan("item,S1,,Cola,Bevrages,10,,,,\n", resolutions: new() { [key] = new ImportResolution(ResolutionChoice.Link, bev.Id) });
        Assert.Empty(plan.NeedsDecision);
        Assert.False(plan.Blocked);
        Assert.Equal(bev.Id, Assert.Single(plan.AutoLinks).ExistingId);
    }

    [Fact]
    public void Stale_resolution_key_is_reported()
    {
        var plan = Plan("item,S1,,Cola,,10,,,,\n", resolutions: new() { ["cat:item:gone"] = new ImportResolution(ResolutionChoice.Create) });
        Assert.Equal(["cat:item:gone"], plan.StaleResolutions);
        Assert.False(plan.Blocked);
    }

    [Fact]
    public void Grouped_category_variants_resolve_once_for_all_lines()
    {
        _ = Cat("Beverages");
        var body = "item,S1,,A,Bevrages,1,,,,\nitem,S2,,B,bevrages!,1,,,,\nitem,S3,,C,BEVRAGES,1,,,,\n";
        var d = Assert.Single(Plan(body).NeedsDecision);
        Assert.Equal([2, 3, 4], d.Lines);

        var plan = Plan(body, resolutions: new() { [d.Key] = new ImportResolution(ResolutionChoice.Create) });
        Assert.Empty(plan.NeedsDecision);
        var cat = Assert.Single(plan.WillCreate, c => c.Kind == PlannedCreateKind.Category);
        Assert.Equal([2, 3, 4], cat.Lines);
    }

    [Fact]
    public void Rename_resolution_rematches_on_new_name()
    {
        var bev = Cat("Beverages");
        var plan = Plan("item,S1,,A,Bevrages,1,,,,\n",
            resolutions: new() { ["cat:item:bevrages"] = new ImportResolution(ResolutionChoice.Rename, NewName: "beverages") });
        Assert.Equal(bev.Id, Assert.Single(plan.AutoLinks).ExistingId);
        Assert.False(plan.Blocked);
    }

    [Fact]
    public void Reason_change_between_replans_keeps_the_key()
    {
        var body = "item,,,Cola,,11,,,,\n";
        var none = Assert.Single(Plan(body).NeedsDecision);
        _ = Rec("Cola");
        _ = Rec("cola!");
        var many = Assert.Single(Plan(body).NeedsDecision);
        Assert.Equal(none.Key, many.Key);

        var plan = Plan(body, resolutions: new() { [none.Key] = new ImportResolution(ResolutionChoice.Create) });
        Assert.Empty(plan.NeedsDecision);
        Assert.Empty(plan.StaleResolutions);
        Assert.Single(plan.WillCreate);
    }

    [Fact]
    public void Link_resolution_on_row_updates_target()
    {
        var r = Rec("Cola", sku: "S1");
        var plan = Plan("item,S2,,Cola,,12,,,,\n", resolutions: new() { ["row:2"] = new ImportResolution(ResolutionChoice.Link, r.Id) });
        var u = Assert.Single(plan.WillUpdate);
        Assert.Equal(r.Id, u.ExistingId);
        Assert.Contains(u.Changes, c => c.Field == "sku" && c.After == "S2");
    }

    [Fact]
    public void Five_thousand_rows_against_five_thousand_records_is_fast()
    {
        var rnd = new Random(1);
        string Word() => new(Enumerable.Range(0, 12).Select(_ => (char)('a' + rnd.Next(26))).ToArray());
        for (var i = 0; i < 5000; i++) _ = Rec(Word(), sku: "E" + i);
        for (var i = 0; i < 200; i++) _ = Cat(Word());
        var sb = new StringBuilder(Header).Append('\n');
        for (var i = 0; i < 5000; i++) sb.Append("item,N").Append(i).Append(",,").Append(Word()).Append(",,1,,,,\n");
        var parsed = CsvImportParser.Parse(new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString())), ImportKind.Catalog);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var plan = ImportPlanner.Plan(parsed, new ExistingCatalog(records, categories));
        sw.Stop();
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), $"took {sw.Elapsed}");
        Assert.Equal(5000, plan.WillCreate.Count(c => c.Kind == PlannedCreateKind.Item) + plan.NeedsDecision.Count);
    }

    private ExistingRecord Rec(string name, string? sku = null, string? barcode = null, ImportRowType type = ImportRowType.Item,
        decimal? price = 10m, decimal? cost = 4m, decimal stock = 0m)
    {
        var r = new ExistingRecord(Guid.NewGuid(), type, sku, barcode, name, null, price, cost, null, null, true, stock);
        records.Add(r);
        return r;
    }

    private ExistingCategory Cat(string name, ImportRowType type = ImportRowType.Item)
    {
        var c = new ExistingCategory(Guid.NewGuid(), type, name);
        categories.Add(c);
        return c;
    }

    [Fact]
    public void Near_match_category_needs_decision_with_nearest()
    {
        var bev = Cat("Beverages");
        var plan = Plan("item,S1,,Cola,Bevrages,10,,,,\n");
        var d = Assert.Single(plan.NeedsDecision);
        Assert.Equal(2, d.Line);
        Assert.Contains(d.Nearest, m => m.Id == bev.Id);
        Assert.True(plan.Blocked);
        Assert.DoesNotContain(plan.WillCreate, c => c.Kind == PlannedCreateKind.Category);
    }

    [Fact]
    public void Identical_category_key_auto_links()
    {
        var c = Cat("Café & Co");
        var plan = Plan("item,S1,,Cola,cafe and co,10,,,,\n");
        var link = Assert.Single(plan.AutoLinks);
        Assert.Equal(c.Id, link.ExistingId);
        Assert.Empty(plan.NeedsDecision);
        Assert.False(plan.Blocked);
    }

    [Fact]
    public void In_file_name_variants_group_into_one_new_category()
    {
        var plan = Plan("item,S1,,A,Snacks & Co,1,,,,\nitem,S2,,B,snacks and co.,1,,,,\n");
        var cat = Assert.Single(plan.WillCreate, c => c.Kind == PlannedCreateKind.Category);
        Assert.Equal("Snacks & Co", cat.Name);
        Assert.Equal(2, plan.WillCreate.Count(c => c.Kind == PlannedCreateKind.Item));
        Assert.False(plan.Blocked);
    }

    [Fact]
    public void Sku_matches_before_barcode()
    {
        var bySku = Rec("Cola", sku: "S1", barcode: "111");
        _ = Rec("Other", sku: "S9", barcode: "222");
        var plan = Plan("item,s1,,Cola,,12,,,,\n");
        var u = Assert.Single(plan.WillUpdate);
        Assert.Equal(bySku.Id, u.ExistingId);
        var change = Assert.Single(u.Changes);
        Assert.Equal("price", change.Field);
        Assert.Equal("10", change.Before);
        Assert.Equal("12", change.After);
    }

    [Fact]
    public void Barcode_match_is_text_and_keeps_leading_zeros()
    {
        _ = Rec("Twelve", sku: "A", barcode: "12345");
        var zero = Rec("Zero", sku: "B", barcode: "0012345");
        var plan = Plan("item,,0012345,Zero,,99,,,,\n");
        Assert.Equal(zero.Id, Assert.Single(plan.WillUpdate).ExistingId);
        Assert.Equal("price", Assert.Single(plan.WillUpdate[0].Changes).Field);
    }

    [Fact]
    public void Scientific_barcode_is_row_error_not_matched()
    {
        _ = Rec("Cola", sku: "S1", barcode: "120000");
        var plan = Plan("item,,1.2E+5,Cola,,,,,,\n");
        Assert.Single(plan.RowErrors);
        Assert.Empty(plan.WillUpdate);
        Assert.True(plan.Blocked);
    }

    [Fact]
    public void Duplicate_sku_in_file_is_row_error()
    {
        var plan = Plan("item,S1,,A,,1,,,,\nitem,s1,,B,,1,,,,\n");
        var e = Assert.Single(plan.RowErrors);
        Assert.Equal(3, e.Line);
        Assert.True(plan.Blocked);
    }

    [Fact]
    public void Barcode_colliding_with_a_different_record_is_row_error()
    {
        _ = Rec("Cola", sku: "S1", barcode: "111");
        _ = Rec("Fanta", sku: "S2", barcode: "222");
        var plan = Plan("item,S1,222,Cola,,,,,,\n");
        Assert.Single(plan.RowErrors);
        Assert.Empty(plan.WillUpdate);
    }

    [Fact]
    public void Sku_collision_with_record_of_other_type_is_row_error()
    {
        _ = Rec("Flour", sku: "S1", type: ImportRowType.Ingredient);
        var plan = Plan("item,S1,,Flour,,,,,,\n");
        Assert.Single(plan.RowErrors);
    }

    [Fact]
    public void Clear_cost_and_zero_price_produce_changes_blank_does_not()
    {
        _ = Rec("Cola", sku: "S1", price: 10m, cost: 4m);
        var plan = Plan("item,S1,,,,0,CLEAR,,,\n");
        var u = Assert.Single(plan.WillUpdate);
        Assert.Contains(u.Changes, c => c.Field == "price" && c.After == "0");
        Assert.Contains(u.Changes, c => c.Field == "cost" && c.Before == "4" && c.After == null);
        Assert.Equal(2, u.Changes.Count);
    }

    [Fact]
    public void New_row_requires_type_and_name()
    {
        var plan = Plan(",S1,,A,,,,,,\nitem,S2,,,,,,,,\n");
        Assert.Equal(2, plan.RowErrors.Count);
    }

    [Fact]
    public void No_identity_row_with_one_name_match_updates_that_record()
    {
        var r = Rec("Cola");
        _ = Rec("Cola", type: ImportRowType.Ingredient);
        var plan = Plan("item,,,COLA,,11,,,,\n");
        Assert.Equal(r.Id, Assert.Single(plan.WillUpdate).ExistingId);
        Assert.Empty(plan.NeedsDecision);
    }

    [Fact]
    public void No_identity_row_with_no_or_many_name_matches_needs_decision()
    {
        _ = Rec("Cola");
        _ = Rec("cola!");
        var plan = Plan("item,,,Cola,,11,,,,\nitem,,,Brand New,,1,,,,\n");
        Assert.Equal(2, plan.NeedsDecision.Count);
        Assert.Empty(plan.WillUpdate);
        Assert.Empty(plan.WillCreate);
        Assert.True(plan.Blocked);
    }

    [Fact]
    public void New_item_near_existing_item_name_needs_decision()
    {
        _ = Rec("Coca Cola", sku: "S1");
        var plan = Plan("item,S2,,Coca Colla,,1,,,,\n");
        Assert.Single(plan.NeedsDecision);
    }

    [Fact]
    public void Stock_import_unknown_identity_is_row_error_and_known_updates()
    {
        _ = Rec("Cola", sku: "S1", stock: 5m);
        var plan = Plan("S1,,8,\nS404,,1,\n,,3,\n", ImportKind.Stock, "sku,barcode,counted_qty,note");
        var u = Assert.Single(plan.WillUpdate);
        Assert.Equal("counted_qty", Assert.Single(u.Changes).Field);
        Assert.Equal(2, plan.RowErrors.Count);
        Assert.Empty(plan.WillCreate);
    }
}
