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
/// Cash/BankTransfer/ManualGcashQr/UtangCredit only for now — each is a
/// single full payment (no split-tender, no partial payments yet). QrPh
/// (needs a live Xendit webhook) and BillPaymentELoad (needs the Dragonpay
/// integration from ADR 0004) are rejected with a clear "not yet available"
/// message rather than faking a confirmation for something that touches
/// real money; Split isn't built yet either.
/// </summary>
public sealed record RecordPaymentRequest(
    PaymentMethod Method,
    decimal? AmountTendered,
    Guid? CustomerCreditLedgerId = null,
    bool AllowCreditLimitOverride = false,
    string? CreditLimitOverrideReason = null);
