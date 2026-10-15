namespace Purch.Application.Reporting;

/// <summary>The small pure calculations the revenue reports share, kept apart so they can be tested without a database.</summary>
public static class ReportMath
{
    /// <summary>The UTC+8 business date and hour that a UTC date and hour fall in. The offset is a whole number of
    /// hours, so shifting a UTC hour bucket is exact.</summary>
    public static (DateOnly Date, int Hour) ToBusinessTime(DateOnly utcDate, int utcHour)
    {
        var shifted = new DateTime(utcDate.Year, utcDate.Month, utcDate.Day, utcHour, 0, 0).Add(ReportTimeZone.Offset);
        return (DateOnly.FromDateTime(shifted), shifted.Hour);
    }

    /// <summary>The share of sale-level net revenue that belongs to the lines with a known cost, spread by line total.
    /// Zero when there are no lines.</summary>
    public static decimal AllocateKnownRevenue(decimal revenueExVat, decimal knownLineGross, decimal lineGross) =>
        lineGross > 0m ? Math.Round(revenueExVat * knownLineGross / lineGross, 2) : 0m;

    /// <summary>One decimal place, or null when the whole is not positive (a margin on no revenue is not 0%).</summary>
    public static decimal? Percent(decimal part, decimal whole) => whole > 0m ? Math.Round(part / whole * 100m, 1) : null;
}
