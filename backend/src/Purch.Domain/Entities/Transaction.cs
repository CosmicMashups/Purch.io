using Purch.Domain.Common;
using Purch.Domain.Enums;

namespace Purch.Domain.Entities;

public class Transaction : TenantScopedEntity
{
    public Guid BranchId { get; set; }

    public Guid DeviceId { get; set; }

    /// <summary>Client-generated idempotency key for the one-call checkout (see
    /// TransactionService.CheckoutAsync): the same SaleId sent twice (a retry after a
    /// lost response) resolves to this one transaction instead of charging twice.
    /// Null for carts built through the older cart endpoints.</summary>
    public Guid? ClientSaleId { get; set; }

    /// <summary>Sequential per branch/device, server-generated — see ReceiptSequence.
    /// Null until payment, so more than one open/voided/kiosk-pending transaction
    /// can coexist per device without tripping the (TenantId, BranchId, DeviceId,
    /// ReceiptNumber) uniqueness, which is only meant to guard issued receipts.</summary>
    public long? ReceiptNumber { get; set; }

    /// <summary>The Z-reading (the device's ResetCounter at the time) that reported this sale, or null
    /// while it hasn't been reported yet; 0 marks sales reported by a Z-reading from before this was
    /// tracked. A reading covers every completed sale not yet reported rather than "receipt numbers above
    /// the last reading", so a sale that reaches the server late (an offline sale syncing after a later
    /// number was already read) lands in the next reading instead of falling through the gap.</summary>
    public int? ZReadingNumber { get; set; }

    /// <summary>Null until a kiosk-originated order is claimed by a cashier — a kiosk
    /// terminal has no staff user, so this can't be required at creation time the
    /// way a POS-originated cart's is.</summary>
    public Guid? StaffUserId { get; set; }

    public TransactionStatus Status { get; set; } = TransactionStatus.Open;

    /// <summary>When this sale was refunded, or null — the precise moment a Z-reading needs to decide
    /// whether a refund falls in its window (see BirReadingService), since the refund can happen long
    /// after the sale's own CreatedAt and often after the sale was already reported on an earlier reading.</summary>
    public DateTimeOffset? RefundedAt { get; set; }

    /// <summary>The terminal that paid the refund out, whose cash drawer it came from. Null for older refunds.</summary>
    public Guid? RefundedOnDeviceId { get; set; }

    /// <summary>When the sale was paid and its receipt issued, or null while it is still an open cart. CreatedAt is when the
    /// cart was started, which can be long before; a receipt shows this moment.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal DiscountAmount { get; set; }

    public bool SeniorPwdDiscountApplied { get; set; }

    /// <summary>The 12% VAT taken off a Senior Citizen/PWD sale (RA 9994, RA 10754): prices include VAT, and the 20% discount
    /// is then worked on the VAT-exclusive price. TotalAmount = gross - VatExemptAmount - DiscountAmount. Zero for any other sale,
    /// including sales rung up before this existed.</summary>
    public decimal VatExemptAmount { get; set; }

    /// <summary>The applied PromoCode.Code, or null if none — re-resolved against
    /// PromoCode on every recalculation rather than caching the discount rule.</summary>
    public string? PromoCode { get; set; }

    /// <summary>Just the promo-code portion of DiscountAmount, for receipt breakdown.</summary>
    public decimal PromoDiscountAmount { get; set; }

    /// <summary>Sum of all lines' PromoDiscountAmount (automatic BOGO/combo/item-discount
    /// promos), for receipt/cart display. Applied before Senior/PWD and PromoCode.</summary>
    public decimal ItemPromoDiscountAmount { get; set; }

    public string? OrderType { get; set; }

    /// <summary>What the customer chose on the kiosk ("cash", "card", "ewallet", "discount"), shown to the
    /// cashier and printed on the receipt. Informational only: the cashier records the real payment.</summary>
    public string? KioskPaymentPreference { get; set; }

    /// <summary>With a "discount" preference, the discount the customer will ask for ("senior", "pwd",
    /// "other"). The kiosk never applies it; the cashier does through the normal Senior/PWD flow.</summary>
    public string? KioskDiscountHint { get; set; }

    /// <summary>Kiosk-originated orders are prep-only — set true, payment always finalized at cashier POS.</summary>
    public bool OriginatedFromKiosk { get; set; }

    /// <summary>Customer-facing pickup number issued when a kiosk order is submitted
    /// (see KioskPrepSequence) — a separate series from ReceiptNumber, since a prep
    /// number is issued before payment and ReceiptNumber must only ever correspond
    /// to a completed, paid sale (BIR requirement). 0 means unissued.</summary>
    public long KioskPrepNumber { get; set; }

    /// <summary>Only meaningful when OriginatedFromKiosk is true — Kitchen Display
    /// advances it, Order Board reads it, both orthogonal to Status (payment).</summary>
    public KitchenStatus KitchenStatus { get; set; } = KitchenStatus.Queued;
}
