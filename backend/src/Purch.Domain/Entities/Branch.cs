using Purch.Domain.Common;
using Purch.Domain.Enums;

namespace Purch.Domain.Entities;

public class Branch : TenantScopedEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Address { get; set; }

    public ReceiptPrinterProfile ReceiptPrinterProfile { get; set; } = ReceiptPrinterProfile.None;

    public bool CashDrawerEnabled { get; set; }

    public CashDrawerPolicy CashDrawerPolicy { get; set; } = CashDrawerPolicy.KickOnSaleOnly;

    // --- Manual GCash QR (D5) — a merchant-uploaded static QR Ph code, paid
    // directly into the tenant's own GCash account with no gateway/fee and
    // no webhook confirmation (cashier verifies receipt manually, same trust
    // model as Bank Transfer). Independent of Xendit's dynamic QR Ph. ---
    public string? ManualGcashQrImageUrl { get; set; }

    public string? ManualGcashAccountName { get; set; }

    public string? ManualGcashAccountNumber { get; set; }
}
