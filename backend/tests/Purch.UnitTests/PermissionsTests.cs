using Purch.Application.Auth;
using Purch.Domain.Enums;

namespace Purch.UnitTests;

public sealed class PermissionsTests
{
    private static readonly string[] All =
        [Permissions.CartPark, Permissions.DiscountApply, Permissions.PriceOverride, Permissions.OpenItemAdd, Permissions.SplitPay];

    [Fact]
    public void Cashier_may_attempt_discount_and_override_but_the_pin_approves()
    {
        var set = Permissions.For(Role.Cashier);
        Assert.Contains(Permissions.CartPark, set);
        Assert.Contains(Permissions.OpenItemAdd, set);
        Assert.Contains(Permissions.SplitPay, set);
        Assert.Contains(Permissions.DiscountApply, set);
        Assert.Contains(Permissions.PriceOverride, set);
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
        Assert.Equal(["Admin", "Cashier", "Manager"], Permissions.RolesFor(Permissions.DiscountApply).OrderBy(r => r));
        Assert.DoesNotContain("Warehouse", Permissions.RolesFor(Permissions.PriceOverride));
    }
}
