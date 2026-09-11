namespace Purch.Domain.Enums;

/// <summary>
/// No Card, no per-wallet methods (GCash/Maya) — digital payment is consolidated
/// behind QR Ph (one PSP-agnostic scan). See docs/adr and the implementation plan.
/// </summary>
public enum PaymentMethod
{
    Cash,
    QrPh,
    BankTransfer,
    BillPaymentELoad,
    UtangCredit,
    Split,
}
