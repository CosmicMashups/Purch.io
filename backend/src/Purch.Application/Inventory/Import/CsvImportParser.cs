using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Purch.Domain.Enums;

namespace Purch.Application.Inventory.Import;

public enum ImportRowType
{
    Item = 0,
    Ingredient = 1,
}

/// <summary>The whole file is unusable (size, row cap, encoding, header). Row-level problems are not thrown; they are
/// collected on <see cref="ParsedRow.Errors"/>.</summary>
public sealed class ImportFormatException(string message) : Exception(message);

/// <summary>One data row. Null = blank cell = leave unchanged. <see cref="ClearCost"/> is the literal CLEAR in cost.
/// <see cref="Line"/> is the 1-based physical line the record starts on (header is line 1).</summary>
public sealed record ParsedRow
{
    public int Line { get; init; }
    public ImportRowType? Type { get; init; }
    public string? Sku { get; init; }
    public string? Barcode { get; init; }
    public string? Name { get; init; }
    public string? Category { get; init; }
    public decimal? Price { get; init; }
    public decimal? Cost { get; init; }
    public bool ClearCost { get; init; }
    public decimal? ReorderThreshold { get; init; }
    public string? Unit { get; init; }
    public bool? Active { get; init; }
    public decimal? CountedQty { get; init; }
    public string? Note { get; init; }
    public List<string> Errors { get; init; } = [];
}

public sealed record ParsedImport(ImportKind Kind, IReadOnlyList<ParsedRow> Rows);

/// <summary>RFC 4180 CSV parser for inventory imports (see <see cref="ImportColumns"/> for the column contract).</summary>
public static partial class CsvImportParser
{
    public const int MaxBytes = 5 * 1024 * 1024;
    public const int MaxRows = 5000;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>What a spreadsheet writes for a long number: always with an exponent sign or a decimal point
    /// (1.2E+5, 1E+10, 1.23457E11). Plain 12E5 / 1E10 are legitimate codes.</summary>
    [GeneratedRegex(@"^([0-9]+(\.[0-9]+)?[eE][+-][0-9]+|[0-9]+\.[0-9]+[eE][0-9]+)$")]
    private static partial Regex ScientificNotation();

    public static ParsedImport Parse(Stream stream, ImportKind kind)
    {
        var text = ReadText(stream);
        var records = ReadRecords(text);
        if (records.Count == 0) throw new ImportFormatException("The file is empty.");

        var header = records[0].Fields.Select(h => h.Trim().ToLowerInvariant()).ToList();
        ValidateHeader(header, kind);

        var data = records.Skip(1).Where(r => !(r.Fields.Count == 1 && r.Fields[0].Length == 0)).ToList();
        if (data.Count > MaxRows) throw new ImportFormatException($"The file has more than {MaxRows} data rows.");

        return new ParsedImport(kind, data.Select(r => ToRow(r, header)).ToList());
    }

    private static string ReadText(Stream stream)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int n;
        while ((n = stream.Read(chunk, 0, chunk.Length)) > 0)
        {
            buffer.Write(chunk, 0, n);
            if (buffer.Length > MaxBytes) throw new ImportFormatException("The file is larger than 5 MB.");
        }

        var bytes = buffer.GetBuffer().AsSpan(0, (int)buffer.Length);
        if (bytes.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF])) bytes = bytes[3..];
        try
        {
            return StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            throw new ImportFormatException("The file is not valid UTF-8.");
        }
    }

    private sealed record Record(int Line, List<string> Fields);

    private static List<Record> ReadRecords(string text)
    {
        var records = new List<Record>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var line = 1;
        var recordLine = 1;
        var inQuotes = false;
        var any = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            any = true;
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { _ = field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else
                {
                    if (c == '\n') line++;
                    _ = field.Append(c);
                }

                continue;
            }

            switch (c)
            {
                case '"' when field.Length == 0:
                    inQuotes = true;
                    break;
                case ',':
                    fields.Add(field.ToString());
                    _ = field.Clear();
                    break;
                case '\r' when i + 1 < text.Length && text[i + 1] == '\n':
                    break;
                case '\n':
                case '\r':
                    fields.Add(field.ToString());
                    _ = field.Clear();
                    records.Add(new Record(recordLine, fields));
                    fields = [];
                    line++;
                    recordLine = line;
                    any = false;
                    break;
                default:
                    _ = field.Append(c);
                    break;
            }
        }

        if (inQuotes) throw new ImportFormatException("Unterminated quoted field.");
        if (any)
        {
            fields.Add(field.ToString());
            records.Add(new Record(recordLine, fields));
        }

        return records;
    }

    private static void ValidateHeader(List<string> header, ImportKind kind)
    {
        var allowed = (kind == ImportKind.Catalog ? ImportColumns.Catalog : ImportColumns.Stock).Concat(ImportColumns.ReadOnly).ToHashSet();
        var seen = new HashSet<string>();
        foreach (var h in header)
        {
            if (!allowed.Contains(h)) throw new ImportFormatException($"Unknown column '{h}'.");
            if (!seen.Add(h)) throw new ImportFormatException($"Duplicate column '{h}'.");
        }

        if (!seen.Contains("sku") && !seen.Contains("barcode")) throw new ImportFormatException("Missing required column 'sku' or 'barcode'.");
        string[] required = kind == ImportKind.Catalog ? ["type", "name"] : ["counted_qty"];
        foreach (var r in required)
        {
            if (!seen.Contains(r)) throw new ImportFormatException($"Missing required column '{r}'.");
        }
    }

    private static ParsedRow ToRow(Record record, List<string> header)
    {
        var errors = new List<string>();
        if (record.Fields.Count > header.Count) errors.Add($"Line {record.Line}: too many fields.");

        string? Cell(string column)
        {
            var idx = header.IndexOf(column);
            if (idx < 0 || idx >= record.Fields.Count) return null;
            var v = Unguard(record.Fields[idx]).Trim();
            return v.Length == 0 ? null : v;
        }

        decimal? Number(string column)
        {
            var v = Cell(column);
            if (v is null) return null;
            if (decimal.TryParse(v, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var d))
            {
                if (d >= 0) return d;
                errors.Add($"Line {record.Line}: {column} cannot be negative.");
                return null;
            }

            errors.Add($"Line {record.Line}: {column} '{v}' is not a valid number.");
            return null;
        }

        ImportRowType? type = null;
        var typeText = Cell("type");
        if (typeText is not null)
        {
            if (typeText.Equals("item", StringComparison.OrdinalIgnoreCase)) type = ImportRowType.Item;
            else if (typeText.Equals("ingredient", StringComparison.OrdinalIgnoreCase)) type = ImportRowType.Ingredient;
            else errors.Add($"Line {record.Line}: type must be 'item' or 'ingredient'.");
        }

        string? Code(string column)
        {
            var v = Cell(column);
            if (v is null || !ScientificNotation().IsMatch(v)) return v;
            errors.Add($"Line {record.Line}: {column} '{v}' looks like a number mangled by a spreadsheet (scientific notation). Format the column as text and re-export.");
            return null;
        }

        var sku = Code("sku");
        var barcode = Code("barcode");

        var clearCost = Cell("cost") is { } c && c.Equals("CLEAR", StringComparison.OrdinalIgnoreCase);
        var cost = clearCost ? null : Number("cost");

        bool? active = null;
        var activeText = Cell("active")?.ToLowerInvariant();
        if (activeText is not null)
        {
            if (activeText is "true" or "yes" or "1") active = true;
            else if (activeText is "false" or "no" or "0") active = false;
            else errors.Add($"Line {record.Line}: active must be true or false.");
        }

        return new ParsedRow
        {
            Line = record.Line,
            Type = type,
            Sku = sku,
            Barcode = barcode,
            Name = Cell("name"),
            Category = Cell("category"),
            Price = Number("price"),
            Cost = cost,
            ClearCost = clearCost,
            ReorderThreshold = Number("reorder_threshold"),
            Unit = Cell("unit"),
            Active = active,
            CountedQty = Number("counted_qty"),
            Note = Cell("note"),
            Errors = errors,
        };
    }

    /// <summary>Strip exactly one leading <c>'</c> iff the next char is = + - @ tab or CR (export formula guard).</summary>
    private static string Unguard(string v) =>
        v.Length >= 2 && v[0] == '\'' && v[1] is '=' or '+' or '-' or '@' or '\t' or '\r' ? v[1..] : v;
}
