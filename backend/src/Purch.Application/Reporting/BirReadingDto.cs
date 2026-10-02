using Purch.Domain.Enums;

namespace Purch.Application.Reporting;

/// <summary>Best-effort BIR X-reading (mid-shift, read-only, re-runnable) /
/// Z-reading (end-of-day, advances the reset counter and grand accumulated
/// sales) report for one device. See docs/adr/0005 — every field here is a
/// best-effort mapping pending real BIR accreditation review, not a
/// guarantee of compliance.
/// // TODO(BIR-ACCREDITATION): verify every field below against actual
/// accreditation paperwork before any live deployment.</summary>
public sealed record BirReadingDto(
    BirReadingType Type,
    Guid DeviceId,
    string MachineIdentificationNumber,
    DateTimeOffset GeneratedAt,
    long? BeginningReceiptNumber,
    long? EndingReceiptNumber,
    int TransactionCount,
    decimal GrossSales,
    // TODO(BIR-ACCREDITATION): all sales are treated as VATable here — the
    // Item model has no VAT-exempt/zero-rated classification yet.
    decimal VatableSales,
    decimal VatAmount,
    decimal SeniorPwdDiscountTotal,
    decimal PromoDiscountTotal,
    decimal TotalDiscounts,
    // Exchanges (Adjustment) processed on this device since the last reading: ReplacementTotal minus
    // ReturnedTotal, already folded into GrossSales/NetSales below — broken out here so the reading stays
    // auditable (what actually happened) instead of blending a different kind of event into "sales".
    decimal ExchangeAdjustmentsTotal,
    // Sales originally rung up on this device that were refunded since the last reading, whether or not
    // the sale itself was already reported on an earlier one — a refund is a current-period event even
    // when it reverses an old sale. Already subtracted from GrossSales/NetSales below.
    decimal RefundsTotal,
    decimal NetSales,
    int VoidedCount,
    decimal VoidedAmount,
    decimal OldGrandAccumulatedSales,
    decimal NewGrandAccumulatedSales,
    int ResetCounter,
    // Receipts in this reading numbered at or below the previous Z-reading's ending number: sales that
    // reached the server only after a later number had already been read (e.g. an offline sale syncing
    // late). Listed so the reading discloses them rather than silently absorbing them.
    IReadOnlyList<long> LateReceiptNumbers,
    // Numbers between the previous reading and this one's ending number that have no completed sale on
    // the server — not yet synced, refused, or voided. Capped, for a gap-auditable trail.
    IReadOnlyList<long> MissingReceiptNumbers);
