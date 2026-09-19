namespace Purch.Application.Pos;

/// <summary>
/// A whole sale in one call: the cart the cashier built on the device, plus how it was paid.
/// The server re-prices every line with its own rules (its total is the one recorded), so
/// nothing in here — least of all ExpectedTotal — is trusted for pricing.
/// </summary>
/// <param name="SaleId">Client-generated idempotency key. Sending the same SaleId again — a retry
/// after a lost response — returns the already-completed sale instead of charging twice.</param>
/// <param name="Lines">The cart lines, in the order they were added.</param>
/// <param name="SeniorPwdDiscountApplied">Cashier-verified Senior Citizen/PWD ID; Admin/Manager only, like the cart endpoint.</param>
/// <param name="PromoCode">A cart-level promo code, or null.</param>
/// <param name="OrderType">Fulfillment choice such as "Dine In"/"Take Out", or null.</param>
/// <param name="Payment">A single full payment, exactly as the cart endpoint accepts.</param>
/// <param name="ExpectedTotal">The total the device showed the customer. If the server prices the
/// cart differently, checkout stops with a 409 before charging anything, so the customer never
/// pays a price they were not shown. Null skips the check.</param>
/// <param name="ReceiptNumber">The number the terminal issued for this sale from its own per-device
/// sequence, so the receipt can be printed before the server has seen the sale. Must be unused on
/// this terminal; the sequence's high-water mark moves up to it. Null lets the server issue the next
/// number, as the older cart endpoints do.</param>
/// <param name="OfflineSale">True when the sale was already completed at the counter (receipt handed
/// over, money taken) while the terminal could not reach the server, and is only now being recorded.
/// The customer already paid the price the device showed, so a different server price no longer stops
/// the sale; the server records it under its own pricing instead of refusing it.</param>
/// <param name="SoldAt">When the sale actually happened on the terminal. Only honoured for an
/// OfflineSale, and clamped to the past week and to now, so a late-syncing sale is dated when it was
/// rung up rather than when it reached the server.</param>
public sealed record CheckoutRequest(
    Guid SaleId,
    IReadOnlyList<AddTransactionLineRequest> Lines,
    bool SeniorPwdDiscountApplied,
    string? PromoCode,
    string? OrderType,
    RecordPaymentRequest Payment,
    decimal? ExpectedTotal = null,
    long? ReceiptNumber = null,
    bool OfflineSale = false,
    DateTimeOffset? SoldAt = null);
