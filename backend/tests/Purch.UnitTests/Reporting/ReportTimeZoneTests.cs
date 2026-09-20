using Purch.Application.Reporting;

namespace Purch.UnitTests.Reporting;

public sealed class ReportTimeZoneTests
{
    [Fact]
    public void The_report_day_starts_at_local_midnight_not_utc_midnight()
    {
        // 20:00 UTC on 1 March is 04:00 on 2 March in Manila, so that instant belongs to 2 March.
        var now = new DateTimeOffset(2026, 3, 1, 20, 0, 0, TimeSpan.Zero);

        var start = ReportTimeZone.StartOfDay(now);

        Assert.Equal(new DateTimeOffset(2026, 3, 1, 16, 0, 0, TimeSpan.Zero), start);
        Assert.Equal(TimeSpan.Zero, start.Offset);
        Assert.Equal(new DateOnly(2026, 3, 2), DateOnly.FromDateTime(now.ToOffset(ReportTimeZone.Offset).DateTime));
    }

    [Fact]
    public void An_early_morning_UTC_instant_still_belongs_to_the_same_local_day()
    {
        // 03:00 UTC on 1 March is 11:00 on 1 March in Manila.
        var now = new DateTimeOffset(2026, 3, 1, 3, 0, 0, TimeSpan.Zero);

        Assert.Equal(new DateTimeOffset(2026, 2, 28, 16, 0, 0, TimeSpan.Zero), ReportTimeZone.StartOfDay(now));
    }
}
