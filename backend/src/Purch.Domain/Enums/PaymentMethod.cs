namespace Purch.Domain.Enums;

/// <summary>
/// No Card, no per-wallet methods (GCash/Maya) as gateway-processed payments —
/// digital gateway payment is consolidated behind QR Ph (one PSP-agnostic
/// scan). ManualGcashQr is not a gateway integration: it's a merchant-
/// uploaded static QR Ph code paid straight into the tenant's own account,
/// confirmed by the cashier the same way BankTransfer is. See docs/adr and
/// the implementation plan.
/// </summary>
public enum PaymentMethod
{
    Cash,
    QrPh,
    BankTransfer,
    ManualGcashQr,
    BillPaymentELoad,
    UtangCredit,
    Split,
}
