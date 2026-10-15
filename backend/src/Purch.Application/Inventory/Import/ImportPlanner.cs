using System.Globalization;
using Purch.Domain.Enums;

namespace Purch.Application.Inventory.Import;

/// <summary>An existing item or ingredient as the planner sees it.</summary>
public sealed record ExistingRecord(
    Guid Id, ImportRowType Type, string? Sku, string? Barcode, string Name, string? Category,
    decimal? Price, decimal? Cost, decimal? ReorderThreshold, string? Unit, bool Active, decimal CurrentStock);

/// <summary>An existing item category (Type Item) or inventory category (Type Ingredient).</summary>
public sealed record ExistingCategory(Guid Id, ImportRowType Type, string Name);

public sealed record ExistingCatalog(IReadOnlyList<ExistingRecord> Records, IReadOnlyList<ExistingCategory> Categories);

public enum PlannedCreateKind
{
    Category = 0,
    Item = 1,
    Ingredient = 2,
}

/// <summary>A creation listed for explicit confirmation. Category creations list every line that uses them.</summary>
public sealed record PlannedCreate(PlannedCreateKind Kind, string Name, IReadOnlyList<int> Lines, ImportRowType Type);

/// <summary>A category name whose key equals an existing category's key; linked automatically, shown so it can be undone.</summary>
public sealed record CategoryAutoLink(int Line, string CsvName, Guid ExistingId, string ExistingName);

public sealed record NearMatch(Guid Id, string Name, int Distance);

public enum DecisionReason
{
    /// <summary>A new category name is within edit distance 2 of an existing category.</summary>
    SimilarCategory = 0,

    /// <summary>A new item/ingredient name is within edit distance 2 of an existing one of the same type.</summary>
    SimilarName = 1,

    /// <summary>A row without SKU/barcode whose name matched no or several existing records.</summary>
    NoIdentity = 2,
}

public sealed record ImportDecision(int Line, DecisionReason Reason, string Name, IReadOnlyList<NearMatch> Nearest);

public sealed record FieldChange(string Field, string? Before, string? After);

public sealed record PlannedUpdate(int Line, Guid ExistingId, ImportRowType Type, IReadOnlyList<FieldChange> Changes);

public sealed record RowError(int Line, string Message);

public sealed record ImportPlan(
    IReadOnlyList<PlannedCreate> WillCreate,
    IReadOnlyList<CategoryAutoLink> AutoLinks,
    IReadOnlyList<ImportDecision> NeedsDecision,
    IReadOnlyList<PlannedUpdate> WillUpdate,
    IReadOnlyList<RowError> RowErrors)
{
    /// <summary>Commit is blocked while any row error or unresolved decision exists.</summary>
    public bool Blocked => RowErrors.Count > 0 || NeedsDecision.Count > 0;
}

/// <summary>Pure matcher: turns parsed rows plus a snapshot of the catalog into a reviewable plan.
/// Match order: SKU (case-insensitive), then barcode (text), then — only when the row has neither — NameKey within
/// the same type when exactly one record matches.</summary>
public static class ImportPlanner
{
    private const int NearDistance = 2;

    public static ImportPlan Plan(ParsedImport parsed, ExistingCatalog existing)
    {
        var creates = new List<PlannedCreate>();
        var links = new List<CategoryAutoLink>();
        var decisions = new List<ImportDecision>();
        var updates = new List<PlannedUpdate>();
        var errors = new List<RowError>();

        var bySku = existing.Records.Where(r => r.Sku is { Length: > 0 })
            .GroupBy(r => r.Sku!.Trim(), StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var byBarcode = existing.Records.Where(r => r.Barcode is { Length: > 0 })
            .GroupBy(r => r.Barcode!.Trim(), StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var seenSku = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var seenBarcode = new Dictionary<string, int>(StringComparer.Ordinal);

        // New categories grouped by (type, key): first spelling wins, all lines listed.
        var newCategories = new Dictionary<(ImportRowType, string), (string Name, List<int> Lines)>();
        var categoryDecided = new HashSet<(ImportRowType, string)>();

        foreach (var row in parsed.Rows)
        {
            var rowErrors = new List<string>(row.Errors);

            if (row.Sku is { } sku && !seenSku.TryAdd(sku, row.Line))
                rowErrors.Add($"Line {row.Line}: SKU '{sku}' is duplicated in the file (first on line {seenSku[sku]}).");
            if (row.Barcode is { } bc && !seenBarcode.TryAdd(bc, row.Line))
                rowErrors.Add($"Line {row.Line}: barcode '{bc}' is duplicated in the file (first on line {seenBarcode[bc]}).");

            var skuHit = row.Sku is null ? null : bySku.GetValueOrDefault(row.Sku);
            var barcodeHit = row.Barcode is null ? null : byBarcode.GetValueOrDefault(row.Barcode);
            var match = skuHit ?? barcodeHit;

            if (skuHit is not null && barcodeHit is not null && skuHit.Id != barcodeHit.Id)
                rowErrors.Add($"Line {row.Line}: barcode '{row.Barcode}' already belongs to '{barcodeHit.Name}'.");
            if (match is not null && row.Type is { } t && t != match.Type)
                rowErrors.Add($"Line {row.Line}: '{match.Name}' already exists as {match.Type.ToString().ToLowerInvariant()} with this SKU/barcode.");

            if (rowErrors.Count > 0)
            {
                errors.AddRange(rowErrors.Select(m => new RowError(row.Line, m)));
                continue;
            }

            if (parsed.Kind == ImportKind.Stock)
            {
                PlanStockRow(row, match, updates, errors);
                continue;
            }

            if (match is null && row.Sku is null && row.Barcode is null)
            {
                // Identity by name only, within the declared type.
                var key = NameKey.Of(row.Name);
                if (row.Type is null || key.Length == 0)
                {
                    errors.Add(new RowError(row.Line, $"Line {row.Line}: a row without SKU or barcode needs type and name."));
                    continue;
                }

                var sameName = existing.Records.Where(r => r.Type == row.Type && NameKey.Of(r.Name) == key).ToList();
                if (sameName.Count == 1)
                {
                    match = sameName[0];
                }
                else
                {
                    var nearest = sameName.Count > 0
                        ? sameName.Select(r => new NearMatch(r.Id, r.Name, 0)).ToList()
                        : Nearest(key, existing.Records.Where(r => r.Type == row.Type).Select(r => (r.Id, r.Name)));
                    decisions.Add(new ImportDecision(row.Line, DecisionReason.NoIdentity, row.Name!, nearest));
                    continue;
                }
            }

            var type = match?.Type ?? row.Type;
            if (match is null && (type is null || row.Name is null))
            {
                errors.Add(new RowError(row.Line, $"Line {row.Line}: a new row needs type (item or ingredient) and name."));
                continue;
            }

            PlanCategory(row, type!.Value, match, existing, links, decisions, newCategories, categoryDecided);

            if (match is not null)
            {
                updates.Add(new PlannedUpdate(row.Line, match.Id, match.Type, Diff(row, match)));
                continue;
            }

            var nameKey = NameKey.Of(row.Name);
            var similar = Nearest(nameKey, existing.Records.Where(r => r.Type == type).Select(r => (r.Id, r.Name)));
            if (similar.Count > 0)
            {
                decisions.Add(new ImportDecision(row.Line, DecisionReason.SimilarName, row.Name!, similar));
                continue;
            }

            creates.Add(new PlannedCreate(
                type == ImportRowType.Item ? PlannedCreateKind.Item : PlannedCreateKind.Ingredient, row.Name!, [row.Line], type!.Value));
        }

        creates.InsertRange(0, newCategories.Select(kv => new PlannedCreate(PlannedCreateKind.Category, kv.Value.Name, kv.Value.Lines, kv.Key.Item1)));
        return new ImportPlan(creates, links, decisions, updates, errors);
    }

    private static void PlanStockRow(ParsedRow row, ExistingRecord? match, List<PlannedUpdate> updates, List<RowError> errors)
    {
        if (match is null)
        {
            var what = row.Sku is null && row.Barcode is null ? "needs a SKU or barcode" : "does not match any existing item or ingredient";
            errors.Add(new RowError(row.Line, $"Line {row.Line}: stock row {what}."));
            return;
        }

        if (row.CountedQty is not { } counted)
        {
            errors.Add(new RowError(row.Line, $"Line {row.Line}: counted_qty is required."));
            return;
        }

        updates.Add(new PlannedUpdate(row.Line, match.Id, match.Type, [new FieldChange("counted_qty", Fmt(match.CurrentStock), Fmt(counted))]));
    }

    private static void PlanCategory(
        ParsedRow row, ImportRowType type, ExistingRecord? match, ExistingCatalog existing,
        List<CategoryAutoLink> links, List<ImportDecision> decisions,
        Dictionary<(ImportRowType, string), (string Name, List<int> Lines)> newCategories, HashSet<(ImportRowType, string)> categoryDecided)
    {
        var key = NameKey.Of(row.Category);
        if (key.Length == 0) return;
        if (match is not null && NameKey.Of(match.Category) == key) return;

        var sameType = existing.Categories.Where(c => c.Type == type).ToList();
        var exact = sameType.FirstOrDefault(c => NameKey.Of(c.Name) == key);
        if (exact is not null)
        {
            if (!string.Equals(exact.Name, row.Category, StringComparison.Ordinal))
                links.Add(new CategoryAutoLink(row.Line, row.Category!, exact.Id, exact.Name));
            return;
        }

        if (newCategories.TryGetValue((type, key), out var pending))
        {
            pending.Lines.Add(row.Line);
            return;
        }

        var near = Nearest(key, sameType.Select(c => (c.Id, c.Name)));
        if (near.Count > 0)
        {
            // One decision per distinct near-match name; later rows with the same key follow that decision.
            if (categoryDecided.Add((type, key)))
                decisions.Add(new ImportDecision(row.Line, DecisionReason.SimilarCategory, row.Category!, near));
            return;
        }

        newCategories[(type, key)] = (row.Category!, [row.Line]);
    }

    private static List<NearMatch> Nearest(string key, IEnumerable<(Guid Id, string Name)> candidates) =>
        candidates
            .Select(c => new NearMatch(c.Id, c.Name, NameKey.Distance(key, NameKey.Of(c.Name))))
            .Where(m => m.Distance <= NearDistance)
            .OrderBy(m => m.Distance).ThenBy(m => m.Name, StringComparer.Ordinal)
            .Take(5)
            .ToList();

    private static List<FieldChange> Diff(ParsedRow row, ExistingRecord e)
    {
        var changes = new List<FieldChange>();
        void Text(string field, string? after, string? before, StringComparison cmp = StringComparison.Ordinal)
        {
            if (after is not null && !string.Equals(after, before, cmp)) changes.Add(new FieldChange(field, before, after));
        }

        void Num(string field, decimal? after, decimal? before)
        {
            if (after is { } a && a != before) changes.Add(new FieldChange(field, before is { } b ? Fmt(b) : null, Fmt(a)));
        }

        Text("sku", row.Sku, e.Sku, StringComparison.OrdinalIgnoreCase);
        Text("barcode", row.Barcode, e.Barcode);
        Text("name", row.Name, e.Name);
        if (row.Category is not null && NameKey.Of(row.Category) != NameKey.Of(e.Category))
            changes.Add(new FieldChange("category", e.Category, row.Category));
        Num("price", row.Price, e.Price);
        if (row.ClearCost)
        {
            if (e.Cost is { } c) changes.Add(new FieldChange("cost", Fmt(c), null));
        }
        else
        {
            Num("cost", row.Cost, e.Cost);
        }

        Num("reorder_threshold", row.ReorderThreshold, e.ReorderThreshold);
        Text("unit", row.Unit, e.Unit);
        if (row.Active is { } act && act != e.Active) changes.Add(new FieldChange("active", e.Active ? "true" : "false", act ? "true" : "false"));
        return changes;
    }

    private static string Fmt(decimal d) => d.ToString("0.####", CultureInfo.InvariantCulture);
}
