namespace Purch.Application.Pos;

/// <summary>
/// A whole kiosk order in one call: the cart the customer built entirely on the kiosk's own screen
/// (nothing posted to the server per tap), plus how to fulfill it. The server re-prices every line
/// with its own rules exactly as AddLineAsync would, so an unavailable item or a changed price is
/// caught here, not after the order is already queued for the kitchen.
/// </summary>
/// <param name="OrderId">Client-generated idempotency key. Sending the same OrderId again — a retry
/// after a lost response — returns the already-placed order instead of placing it twice.</param>
/// <param name="Lines">The cart lines the kiosk built locally, in the order they were added.</param>
/// <param name="OrderType">Fulfillment choice such as "Dine In"/"Take Out".</param>
/// <param name="PaymentPreference">How the customer says they will pay at the counter: one of
/// <see cref="KioskPaymentPreferences.All"/>, or null. Informational only — the cashier still takes the
/// real payment, and it never changes a price.</param>
/// <param name="DiscountHint">Only with a "discount" preference: which discount the customer will ask
/// for (one of <see cref="KioskPaymentPreferences.DiscountHints"/>). The cashier applies the real discount.</param>
public sealed record PlaceKioskOrderRequest(
    Guid OrderId,
    IReadOnlyList<AddTransactionLineRequest> Lines,
    string OrderType,
    string? PaymentPreference = null,
    string? DiscountHint = null);

/// <summary>The values a kiosk may send as its payment preference and discount hint.</summary>
public static class KioskPaymentPreferences
{
    public const string Cash = "cash";
    public const string Card = "card";
    public const string EWallet = "ewallet";
    public const string Discount = "discount";

    public static readonly IReadOnlyList<string> All = [Cash, Card, EWallet, Discount];

    public static readonly IReadOnlyList<string> DiscountHints = ["senior", "pwd", "other"];
}
