using Purch.Application.Auth;
using Purch.Domain.Enums;

namespace Purch.UnitTests;

public sealed class PermissionsTests
{
    private static readonly string[] All =
        [Permissions.CartPark, Permissions.DiscountApply, Permissions.PriceOverride, Permissions.OpenItemAdd, Permissions.SplitPay];

    [Fact]
    public void Cashier_gets_park_open_item_and_split_only()
    {
        var set = Permissions.For(Role.Cashier);
        Assert.Contains(Permissions.CartPark, set);
        Assert.Contains(Permissions.OpenItemAdd, set);
        Assert.Contains(Permissions.SplitPay, set);
        Assert.DoesNotContain(Permissions.DiscountApply, set);
        Assert.DoesNotContain(Permissions.PriceOverride, set);
    }

    [Fact]
    public void Warehouse_gets_nothing() => Assert.Empty(Permissions.For(Role.Warehouse));

    [Theory]
    [InlineData(Role.Admin)]
    [InlineData(Role.Manager)]
    public void Admin_and_manager_get_every_key(Role role)
    {
        foreach (var key in All) Assert.Contains(key, Permissions.For(role));
    }

    [Fact]
    public void RolesFor_lists_roles_holding_the_key()
    {
        Assert.Equal(["Admin", "Manager"], Permissions.RolesFor(Permissions.DiscountApply).OrderBy(r => r));
    }
}
