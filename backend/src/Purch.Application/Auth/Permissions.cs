using Purch.Domain.Enums;

namespace Purch.Application.Auth;

/// <summary>Permission keys for POS actions. Today they resolve to the built-in roles; U5 makes
/// <see cref="For"/> data-driven without touching callers.</summary>
public static class Permissions
{
    public const string CartPark = "cart.park";
    public const string DiscountApply = "discount.apply";
    public const string PriceOverride = "price.override";
    public const string OpenItemAdd = "openitem.add";
    public const string SplitPay = "payment.split";

    private static readonly string[] AllKeys = [CartPark, DiscountApply, PriceOverride, OpenItemAdd, SplitPay];

    private static readonly IReadOnlySet<string> Empty = new HashSet<string>();

    private static readonly IReadOnlySet<string> CashierKeys =
        new HashSet<string> { CartPark, OpenItemAdd, SplitPay };

    private static readonly IReadOnlySet<string> AdminKeys = new HashSet<string>(AllKeys);

    public static IReadOnlySet<string> For(Role role) => role switch
    {
        Role.Admin or Role.Manager => AdminKeys,
        Role.Cashier => CashierKeys,
        _ => Empty,
    };

    /// <summary>Role names holding <paramref name="key"/>; the single place routes resolve keys.</summary>
    public static IReadOnlyList<string> RolesFor(string key) =>
        Enum.GetValues<Role>().Where(r => For(r).Contains(key)).Select(r => r.ToString()).ToArray();
}
