using System.Globalization;
using System.Text;

namespace Purch.Application.Reporting;

/// <summary>CSV building shared by the report exports.</summary>
public static class ReportCsv
{
    /// <summary>Quotes a value and defuses spreadsheet formulas: a cell starting with one of these is run as a formula
    /// when the file is opened, and names, reasons and promo codes are user-entered.</summary>
    public static string Field(string value)
    {
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            value = "'" + value;
        }

        return value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }

    public static string Money(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    public static string Time(DateTimeOffset value) => value.UtcDateTime.ToString("o", CultureInfo.InvariantCulture);

    public static string Build(string header, IEnumerable<IEnumerable<string>> rows)
    {
        var csv = new StringBuilder();
        _ = csv.AppendLine(header);
        foreach (var row in rows)
        {
            _ = csv.AppendLine(string.Join(',', row));
        }

        return csv.ToString();
    }
}
