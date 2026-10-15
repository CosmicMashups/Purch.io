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
public sealed record PlannedCreate(
    PlannedCreateKind Kind, string Name, IReadOnlyList<int> Lines, ImportRowType Type,
    string? Key = null, Guid? ResolvedCategoryId = null, string? NewCategoryKey = null);

/// <summary>A category linked to an existing one (identical key, or a Link resolution); shown so it can be undone.</summary>
public sealed record CategoryAutoLink(int Line, string CsvName, Guid ExistingId, string ExistingName);

public sealed record NearMatch(Guid Id, string Name, int Distance);

public enum DecisionReason
{
    /// <summary>A new category name is near an existing category.</summary>
    SimilarCategory = 0,

    /// <summary>A new item/ingredient name is near (or equal to) an existing one of the same type.</summary>
    SimilarName = 1,

    /// <summary>A row without SKU/barcode whose name matched no or several existing records.</summary>
    NoIdentity = 2,
}

/// <summary>An open question. <see cref="Key"/> is stable across re-plans and independent of <see cref="Reason"/>:
/// <c>cat:{type}:{nameKey}</c>, <c>row:{line}</c> (rows with SKU/barcode), <c>name:{type}:{nameKey}</c> (name-only rows).</summary>
public sealed record ImportDecision(string Key, IReadOnlyList<int> Lines, DecisionReason Reason, string Name, IReadOnlyList<NearMatch> Nearest)
{
    public int Line => Lines[0];
}

public enum ResolutionChoice
{
    Link = 0,
    Create = 1,
    Rename = 2,
}

/// <summary>The user's answer to an <see cref="ImportDecision"/>, keyed by <see cref="ImportDecision.Key"/>.
/// Link needs <see cref="TargetId"/>; Rename needs <see cref="NewName"/> (matching re-runs on it).</summary>
public sealed record ImportResolution(ResolutionChoice Choice, Guid? TargetId = null, string? NewName = null);

public sealed record FieldChange(string Field, string? Before, string? After);

public sealed record PlannedUpdate(
    int Line, Guid ExistingId, ImportRowType Type, IReadOnlyList<FieldChange> Changes,
    Guid? ResolvedCategoryId = null, string? NewCategoryKey = null);

public sealed record RowError(int Line, string Message);

public sealed record ImportPlan(
    IReadOnlyList<PlannedCreate> WillCreate,
    IReadOnlyList<CategoryAutoLink> AutoLinks,
    IReadOnlyList<ImportDecision> NeedsDecision,
    IReadOnlyList<PlannedUpdate> WillUpdate,
    IReadOnlyList<RowError> RowErrors,
    int UnchangedCount,
    IReadOnlyList<string> StaleResolutions)
{
    /// <summary>Commit is blocked while any row error or unresolved decision exists.</summary>
    public bool Blocked => RowErrors.Count > 0 || NeedsDecision.Count > 0;
}

/// <summary>Pure matcher: turns parsed rows plus a snapshot of the catalog (and any saved resolutions) into a
/// reviewable plan. Match order: SKU (case-insensitive), then barcode (text), then — only when the row has neither —
/// NameKey within the same type when exactly one record matches.</summary>
public static class ImportPlanner
{
    /// <summary>Rename resolutions are followed at most this many times per row (a renamed name may ask again).</summary>
    private const int MaxRenames = 1;

    public static ImportPlan Plan(ParsedImport parsed, ExistingCatalog existing, IReadOnlyDictionary<string, ImportResolution>? resolutions = null) =>
        new Planner(existing, resolutions ?? new Dictionary<string, ImportResolution>()).Run(parsed);

    private static string T(ImportRowType t) => t == ImportRowType.Item ? "item" : "ingredient";

    private sealed class Planner
    {
        private readonly IReadOnlyDictionary<string, ImportResolution> resolutions;
        private readonly HashSet<string> usedKeys = [];
        private readonly Dictionary<Guid, ExistingRecord> recordsById;
        private readonly Dictionary<string, ExistingRecord> bySku;
        private readonly Dictionary<string, ExistingRecord> byBarcode;
        private readonly Dictionary<(ImportRowType, string), List<ExistingRecord>> recordsByKey;
        private readonly Dictionary<ImportRowType, Lazy<NearMatchIndex>> recordIndex;
        private readonly Dictionary<(ImportRowType, string), ExistingCategory> categoriesByKey;
        private readonly Dictionary<Guid, ExistingCategory> categoriesById;
        private readonly Dictionary<ImportRowType, Lazy<NearMatchIndex>> categoryIndex;

        private readonly List<PlannedCreate> creates = [];
        private readonly List<CategoryAutoLink> links = [];
        private readonly List<ImportDecision> decisions = [];
        private readonly Dictionary<string, int> decisionIndex = [];
        private readonly List<PlannedUpdate> updates = [];
        private readonly List<RowError> errors = [];
        private readonly Dictionary<(ImportRowType, string), (string Name, List<int> Lines)> newCategories = [];
        private int unchanged;

        public Planner(ExistingCatalog existing, IReadOnlyDictionary<string, ImportResolution> resolutions)
        {
            this.resolutions = resolutions;
            recordsById = existing.Records.ToDictionary(r => r.Id);
            bySku = existing.Records.Where(r => r.Sku is { Length: > 0 })
                .GroupBy(r => r.Sku!.Trim(), StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            byBarcode = existing.Records.Where(r => r.Barcode is { Length: > 0 })
                .GroupBy(r => r.Barcode!.Trim(), StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            recordsByKey = existing.Records.GroupBy(r => (r.Type, NameKey.Of(r.Name))).ToDictionary(g => g.Key, g => g.ToList());
            recordIndex = Enum.GetValues<ImportRowType>().ToDictionary(
                t => t, t => new Lazy<NearMatchIndex>(() => new NearMatchIndex(existing.Records.Where(r => r.Type == t).Select(r => (r.Id, r.Name)))));
            categoriesByKey = existing.Categories.GroupBy(c => (c.Type, NameKey.Of(c.Name))).ToDictionary(g => g.Key, g => g.First());
            categoriesById = existing.Categories.ToDictionary(c => c.Id);
            categoryIndex = Enum.GetValues<ImportRowType>().ToDictionary(
                t => t, t => new Lazy<NearMatchIndex>(() => new NearMatchIndex(existing.Categories.Where(c => c.Type == t).Select(c => (c.Id, c.Name)))));
        }

        public ImportPlan Run(ParsedImport parsed)
        {
            var seenSku = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var seenBarcode = new Dictionary<string, int>(StringComparer.Ordinal);

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
                    rowErrors.Add($"Line {row.Line}: '{match.Name}' already exists as {T(match.Type)} with this SKU/barcode.");

                if (rowErrors.Count > 0)
                {
                    errors.AddRange(rowErrors.Select(m => new RowError(row.Line, m)));
                    continue;
                }

                if (parsed.Kind == ImportKind.Stock) PlanStockRow(row, match);
                else PlanCatalogRow(row, match);
            }

            creates.InsertRange(0, newCategories.Select(kv => new PlannedCreate(
                PlannedCreateKind.Category, kv.Value.Name, kv.Value.Lines, kv.Key.Item1, CategoryKey(kv.Key.Item1, kv.Key.Item2))));
            var candidates = CandidateKeys(parsed);
            var stale = resolutions.Keys.Where(k => !usedKeys.Contains(k) && !candidates.Contains(k)).Order(StringComparer.Ordinal).ToList();
            return new ImportPlan(creates, links, decisions, updates, errors, unchanged, stale);
        }

        private static string CategoryKey(ImportRowType type, string nameKey) => $"cat:{T(type)}:{nameKey}";

        /// <summary>Every decision key some row of the file could produce, so a resolution is only stale when no row
        /// could ask that question any more (not merely because the row stopped earlier this time).</summary>
        private static HashSet<string> CandidateKeys(ParsedImport parsed)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in parsed.Rows)
            {
                _ = keys.Add($"row:{row.Line}");
                ImportRowType[] types = row.Type is { } t ? [t] : Enum.GetValues<ImportRowType>();
                var nameKey = NameKey.Of(row.Name);
                var catKey = NameKey.Of(row.Category);
                foreach (var type in types)
                {
                    if (nameKey.Length > 0) _ = keys.Add($"name:{T(type)}:{nameKey}");
                    if (catKey.Length > 0) _ = keys.Add(CategoryKey(type, catKey));
                }
            }

            return keys;
        }

        private ImportResolution? Resolve(string key)
        {
            _ = usedKeys.Add(key);
            return resolutions.GetValueOrDefault(key);
        }

        private void AddDecision(string key, int line, DecisionReason reason, string name, IReadOnlyList<NearMatch> nearest)
        {
            if (decisionIndex.TryGetValue(key, out var i))
            {
                ((List<int>)decisions[i].Lines).Add(line);
                return;
            }

            decisionIndex[key] = decisions.Count;
            decisions.Add(new ImportDecision(key, new List<int> { line }, reason, name, nearest));
        }

        private void Error(int line, string message) => errors.Add(new RowError(line, $"Line {line}: {message}"));

        private void PlanStockRow(ParsedRow row, ExistingRecord? match)
        {
            if (match is null)
            {
                Error(row.Line, row.Sku is null && row.Barcode is null
                    ? "stock row needs a SKU or barcode."
                    : "stock row does not match any existing item or ingredient.");
                return;
            }

            if (row.CountedQty is not { } counted)
            {
                Error(row.Line, "counted_qty is required.");
                return;
            }

            if (counted == match.CurrentStock)
            {
                unchanged++;
                return;
            }

            updates.Add(new PlannedUpdate(row.Line, match.Id, match.Type, [new FieldChange("counted_qty", Fmt(match.CurrentStock), Fmt(counted))]));
        }

        private enum Outcome { Update, Create, Decision, Error }

        private void PlanCatalogRow(ParsedRow row, ExistingRecord? match)
        {
            var name = row.Name;
            var type = match?.Type ?? row.Type;
            Outcome outcome;

            if (match is not null)
            {
                outcome = Outcome.Update;
            }
            else if (type is null || name is null)
            {
                Error(row.Line, row.Sku is null && row.Barcode is null
                    ? "a row without SKU or barcode needs type and name."
                    : "a new row needs type (item or ingredient) and name.");
                return;
            }
            else
            {
                var identity = row.Sku is not null || row.Barcode is not null;
                (outcome, match, name) = identity ? MatchNewIdentityRow(row, type.Value, name) : MatchNameOnlyRow(row, type.Value, name);
            }

            if (outcome is Outcome.Error or Outcome.Decision) return;
            if (!PlanCategory(row, type!.Value, match, out var category)) return;

            if (outcome == Outcome.Update)
            {
                var changes = Diff(row, match!, name, category.Name);
                if (changes.Count == 0) unchanged++;
                else updates.Add(new PlannedUpdate(row.Line, match!.Id, match.Type, changes, category.Id, category.NewKey));
                return;
            }

            creates.Add(new PlannedCreate(
                type == ImportRowType.Item ? PlannedCreateKind.Item : PlannedCreateKind.Ingredient, name!, [row.Line], type!.Value,
                null, category.Id, category.NewKey));
        }

        private ExistingRecord? LinkTarget(ParsedRow row, ImportResolution res, ImportRowType type)
        {
            if (res.TargetId is { } id && recordsById.TryGetValue(id, out var target) && target.Type == type) return target;
            Error(row.Line, $"the chosen existing {T(type)} no longer exists.");
            return null;
        }

        /// <summary>Row has SKU/barcode that matched nothing: a create, unless its name is near an existing record.</summary>
        private (Outcome, ExistingRecord?, string?) MatchNewIdentityRow(ParsedRow row, ImportRowType type, string name)
        {
            var key = $"row:{row.Line}";
            for (var renames = 0; ; renames++)
            {
                var nameKey = NameKey.Of(name);
                if (nameKey.Length == 0)
                {
                    Error(row.Line, "name has no letters or digits.");
                    return (Outcome.Error, null, null);
                }

                var near = recordIndex[type].Value.Find(nameKey);
                if (near.Count == 0) return (Outcome.Create, null, name);

                var res = Resolve(key);
                if (res is null || (res.Choice == ResolutionChoice.Rename && (renames >= MaxRenames || string.IsNullOrWhiteSpace(res.NewName))))
                {
                    AddDecision(key, row.Line, DecisionReason.SimilarName, name, near);
                    return (Outcome.Decision, null, null);
                }

                switch (res.Choice)
                {
                    case ResolutionChoice.Create:
                        return (Outcome.Create, null, name);
                    case ResolutionChoice.Link:
                        var target = LinkTarget(row, res, type);
                        return target is null ? (Outcome.Error, null, null) : (Outcome.Update, target, name);
                    default:
                        name = res.NewName!.Trim();
                        break;
                }
            }
        }

        /// <summary>Row has neither SKU nor barcode: matches by NameKey when exactly one record of the type has it.</summary>
        private (Outcome, ExistingRecord?, string?) MatchNameOnlyRow(ParsedRow row, ImportRowType type, string name)
        {
            for (var renames = 0; ; renames++)
            {
                var nameKey = NameKey.Of(name);
                if (nameKey.Length == 0)
                {
                    Error(row.Line, "name has no letters or digits.");
                    return (Outcome.Error, null, null);
                }

                var same = recordsByKey.GetValueOrDefault((type, nameKey)) ?? [];
                if (same.Count == 1) return (Outcome.Update, same[0], name);

                var key = $"name:{T(type)}:{nameKey}";
                var res = Resolve(key);
                if (res is null || (res.Choice == ResolutionChoice.Rename && (renames >= MaxRenames || string.IsNullOrWhiteSpace(res.NewName))))
                {
                    var nearest = same.Count > 0 ? same.Select(r => new NearMatch(r.Id, r.Name, 0)).ToList() : recordIndex[type].Value.Find(nameKey);
                    AddDecision(key, row.Line, DecisionReason.NoIdentity, name, nearest);
                    return (Outcome.Decision, null, null);
                }

                switch (res.Choice)
                {
                    case ResolutionChoice.Create:
                        return (Outcome.Create, null, name);
                    case ResolutionChoice.Link:
                        var target = LinkTarget(row, res, type);
                        return target is null ? (Outcome.Error, null, null) : (Outcome.Update, target, name);
                    default:
                        name = res.NewName!.Trim();
                        break;
                }
            }
        }

        /// <summary>The row's category after matching/resolutions: an existing category id, or the key of a category
        /// this import creates (<c>cat:{type}:{nameKey}</c>), plus the name to show in diffs. All null = unchanged.</summary>
        private readonly record struct CategoryResult(Guid? Id, string? NewKey, string? Name);

        /// <summary>Plans the row's category. Returns false when the row must stop (error).</summary>
        private bool PlanCategory(ParsedRow row, ImportRowType type, ExistingRecord? match, out CategoryResult result)
        {
            result = default;
            if (row.Category is null) return true;
            var name = row.Category;
            if (match is not null && NameKey.Of(match.Category) == NameKey.Of(name))
            {
                result = new CategoryResult(categoriesByKey.GetValueOrDefault((type, NameKey.Of(name)))?.Id, null, null);
                return true;
            }

            for (var renames = 0; ; renames++)
            {
                var key = NameKey.Of(name);
                if (key.Length == 0)
                {
                    Error(row.Line, "category has no letters or digits.");
                    return false;
                }

                if (categoriesByKey.TryGetValue((type, key), out var exact))
                {
                    if (!string.Equals(exact.Name, row.Category, StringComparison.Ordinal))
                        links.Add(new CategoryAutoLink(row.Line, row.Category, exact.Id, exact.Name));
                    result = new CategoryResult(exact.Id, null, exact.Name);
                    return true;
                }

                if (newCategories.TryGetValue((type, key), out var pending))
                {
                    pending.Lines.Add(row.Line);
                    result = new CategoryResult(null, CategoryKey(type, key), pending.Name);
                    return true;
                }

                var near = categoryIndex[type].Value.Find(key);
                var dkey = CategoryKey(type, key);
                if (near.Count == 0)
                {
                    newCategories[(type, key)] = (name, [row.Line]);
                    result = new CategoryResult(null, dkey, name);
                    return true;
                }

                var res = Resolve(dkey);
                if (res is null || (res.Choice == ResolutionChoice.Rename && (renames >= MaxRenames || string.IsNullOrWhiteSpace(res.NewName))))
                {
                    AddDecision(dkey, row.Line, DecisionReason.SimilarCategory, name, near);
                    result = new CategoryResult(null, null, name);
                    return true; // the item itself still plans; Blocked holds the commit
                }

                switch (res.Choice)
                {
                    case ResolutionChoice.Create:
                        newCategories[(type, key)] = (name, [row.Line]);
                        result = new CategoryResult(null, dkey, name);
                        return true;
                    case ResolutionChoice.Link:
                        if (res.TargetId is { } id && categoriesById.TryGetValue(id, out var target) && target.Type == type)
                        {
                            links.Add(new CategoryAutoLink(row.Line, row.Category, target.Id, target.Name));
                            result = new CategoryResult(target.Id, null, target.Name);
                            return true;
                        }

                        Error(row.Line, "the chosen existing category no longer exists.");
                        return false;
                    default:
                        name = res.NewName!.Trim();
                        break;
                }
            }
        }

        private static List<FieldChange> Diff(ParsedRow row, ExistingRecord e, string? name, string? category)
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
            Text("name", name, e.Name);
            if (category is not null && NameKey.Of(category) != NameKey.Of(e.Category))
                changes.Add(new FieldChange("category", e.Category, category));
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
    }

    private static string Fmt(decimal d) => d.ToString("0.####", CultureInfo.InvariantCulture);
}
