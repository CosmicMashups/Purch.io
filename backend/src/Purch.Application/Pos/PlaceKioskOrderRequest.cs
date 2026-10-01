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
public sealed record PlaceKioskOrderRequest(
    Guid OrderId,
    IReadOnlyList<AddTransactionLineRequest> Lines,
    string OrderType);
