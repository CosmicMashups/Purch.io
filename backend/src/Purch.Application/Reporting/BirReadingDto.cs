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
    decimal NetSales,
    int VoidedCount,
    decimal VoidedAmount,
    decimal OldGrandAccumulatedSales,
    decimal NewGrandAccumulatedSales,
    int ResetCounter);
