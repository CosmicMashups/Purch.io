using Purch.Domain.Common;
using Purch.Domain.Enums;

namespace Purch.Domain.Entities;

public class Transaction : TenantScopedEntity
{
    public Guid BranchId { get; set; }

    public Guid DeviceId { get; set; }

    /// <summary>Sequential per branch/device, server-generated — see ReceiptSequence.
    /// Null until payment, so more than one open/voided/kiosk-pending transaction
    /// can coexist per device without tripping the (TenantId, BranchId, DeviceId,
    /// ReceiptNumber) uniqueness, which is only meant to guard issued receipts.</summary>
    public long? ReceiptNumber { get; set; }

    /// <summary>Null until a kiosk-originated order is claimed by a cashier — a kiosk
    /// terminal has no staff user, so this can't be required at creation time the
    /// way a POS-originated cart's is.</summary>
    public Guid? StaffUserId { get; set; }

    public TransactionStatus Status { get; set; } = TransactionStatus.Open;

    public decimal TotalAmount { get; set; }

    public decimal DiscountAmount { get; set; }

    public bool SeniorPwdDiscountApplied { get; set; }

    /// <summary>The applied PromoCode.Code, or null if none — re-resolved against
    /// PromoCode on every recalculation rather than caching the discount rule.</summary>
    public string? PromoCode { get; set; }

    /// <summary>Just the promo-code portion of DiscountAmount, for receipt breakdown.</summary>
    public decimal PromoDiscountAmount { get; set; }

    public string? OrderType { get; set; }

    /// <summary>Kiosk-originated orders are prep-only — set true, payment always finalized at cashier POS.</summary>
    public bool OriginatedFromKiosk { get; set; }

    /// <summary>Customer-facing pickup number issued when a kiosk order is submitted
    /// (see KioskPrepSequence) — a separate series from ReceiptNumber, since a prep
    /// number is issued before payment and ReceiptNumber must only ever correspond
    /// to a completed, paid sale (BIR requirement). 0 means unissued.</summary>
    public long KioskPrepNumber { get; set; }
}
