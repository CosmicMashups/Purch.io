namespace Purch.Application.Reporting;

/// <summary>The calendar the sales reports count days in. The business runs in the Philippines
/// (UTC+8, no daylight saving), so "today" has to start at local midnight; counting in UTC made the
/// day roll over at 8am. A fixed offset rather than a named time zone: it can't drift and doesn't
/// depend on tzdata being present in a minimal container image.</summary>
public static class ReportTimeZone
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(8);

    /// <summary>Local midnight of the report day that contains <paramref name="now"/>, returned as a
    /// UTC instant: Postgres only accepts offset-zero values as timestamptz query parameters.</summary>
    public static DateTimeOffset StartOfDay(DateTimeOffset now)
    {
        var local = now.ToOffset(Offset);
        return new DateTimeOffset(local.Date, Offset).ToUniversalTime();
    }
}
