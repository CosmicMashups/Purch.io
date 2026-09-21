using Purch.Application.Common;

namespace Purch.UnitTests;

public class PagingTests
{
    [Fact]
    public void Missing_limit_uses_the_default()
    {
        Assert.Equal(Paging.DefaultLimit, Paging.ClampLimit(null));
    }

    [Theory]
    [InlineData(-5, 1)]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(50, 50)]
    [InlineData(500, 500)]
    [InlineData(10_000, Paging.MaxLimit)]
    public void Requested_limit_is_clamped_to_the_allowed_range(int requested, int expected)
    {
        Assert.Equal(expected, Paging.ClampLimit(requested));
    }
}
