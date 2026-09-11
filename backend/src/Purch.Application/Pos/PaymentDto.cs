using Purch.Domain.Enums;

namespace Purch.Application.Pos;

public sealed record PaymentDto(
    Guid Id,
    PaymentMethod Method,
    PaymentStatus Status,
    decimal Amount,
    decimal? AmountTendered,
    decimal? ChangeGiven);

/// <summary>
/// Cash/BankTransfer/ManualGcashQr only for now — each is a single full
/// payment (no split-tender, no partial payments yet). QrPh (needs a live
/// Xendit webhook), BillPaymentELoad (needs the Dragonpay integration from
/// ADR 0004), UtangCredit (Phase 9, per the implementation plan), and Split
/// are rejected with a clear "not yet available" message rather than faking
/// a confirmation for something that touches real money.
/// </summary>
public sealed record RecordPaymentRequest(PaymentMethod Method, decimal? AmountTendered);
