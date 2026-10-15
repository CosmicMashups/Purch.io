using Purch.Application.Common.Exceptions;
using Purch.Application.Reporting;

namespace Purch.UnitTests.Reporting;

public sealed class ReportMathTests
{
    [Theory]
    [InlineData(2026, 10, 15, 0, 2026, 10, 15, 8)]
    [InlineData(2026, 10, 15, 15, 2026, 10, 15, 23)]
    [InlineData(2026, 10, 15, 16, 2026, 10, 16, 0)]
    [InlineData(2026, 12, 31, 20, 2027, 1, 1, 4)]
    [InlineData(2028, 2, 28, 17, 2028, 2, 29, 1)]
    public void A_utc_hour_lands_on_the_correct_business_date_and_hour(int y, int m, int d, int h, int ey, int em, int ed, int eh)
    {
        var (date, hour) = ReportMath.ToBusinessTime(new DateOnly(y, m, d), h);

        Assert.Equal(new DateOnly(ey, em, ed), date);
        Assert.Equal(eh, hour);
    }

    [Fact]
    public void Known_cost_revenue_is_the_known_share_of_the_line_total()
    {
        Assert.Equal(60m, ReportMath.AllocateKnownRevenue(120m, 50m, 100m));
        Assert.Equal(0m, ReportMath.AllocateKnownRevenue(120m, 0m, 100m));
        Assert.Equal(120m, ReportMath.AllocateKnownRevenue(120m, 100m, 100m));
    }

    [Fact]
    public void Known_cost_revenue_is_zero_when_the_period_has_no_lines()
    {
        Assert.Equal(0m, ReportMath.AllocateKnownRevenue(120m, 0m, 0m));
    }

    [Fact]
    public void A_percentage_on_nothing_is_unknown_not_zero()
    {
        Assert.Null(ReportMath.Percent(10m, 0m));
        Assert.Equal(33.3m, ReportMath.Percent(1m, 3m));
        Assert.Equal(0m, ReportMath.Percent(0m, 50m));
    }

    [Fact]
    public void A_range_is_refused_when_empty_reversed_or_over_a_year()
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        ReportRange.Validate(start, start.AddDays(366));
        _ = Assert.Throws<ValidationException>(() => ReportRange.Validate(start, start));
        _ = Assert.Throws<ValidationException>(() => ReportRange.Validate(start, start.AddDays(-1)));
        _ = Assert.Throws<ValidationException>(() => ReportRange.Validate(start, start.AddDays(367)));
    }

    [Theory]
    [InlineData("=1+1", "'=1+1")]
    [InlineData("plain", "plain")]
    [InlineData("a,b", "\"a,b\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    public void The_csv_field_defuses_formulas_and_quotes_special_characters(string input, string expected)
    {
        Assert.Equal(expected, ReportCsv.Field(input));
    }
}
