using Microsoft.Extensions.Configuration;
using Purch.Api;

namespace Purch.UnitTests;

public sealed class PosSettingsTests
{
    private static PosSettings With(params (string Key, string Value)[] values) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(values.ToDictionary(v => v.Key, v => (string?)v.Value)).Build());

    [Fact]
    public void The_total_the_customer_was_shown_is_required_unless_someone_turns_it_off()
    {
        Assert.True(With().RequireExpectedTotal);
        Assert.True(With(("POS_REQUIRE_EXPECTED_TOTAL", "true")).RequireExpectedTotal);
        Assert.False(With(("POS_REQUIRE_EXPECTED_TOTAL", "false")).RequireExpectedTotal);
    }
}
