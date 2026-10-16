using Purch.Domain.Enums;

namespace Purch.Application.Pos;

public sealed record PaymentDto(
    Guid Id,
    PaymentMethod Method,
    PaymentStatus Status,
    decimal Amount,
    decimal? AmountTendered,
    decimal? ChangeGiven,
    int Sequence = 0,
    string? Reference = null);

/// <summary>One tender of a split payment. Only a Cash part may be tendered above its Amount (the excess is change);
/// a non-cash part's AmountTendered must be null or equal to Amount. See SplitTenderService for every rule.</summary>
public sealed record PaymentPartRequest(
    PaymentMethod Method,
    decimal Amount,
    decimal? AmountTendered = null,
    Guid? CustomerCreditLedgerId = null,
    string? Reference = null);

/// <summary>
/// Cash/BankTransfer/ManualGcashQr/UtangCredit only for now — each is a
/// single full payment (no split-tender, no partial payments yet). QrPh
/// (needs a live Xendit webhook) and BillPaymentELoad (needs the Dragonpay
/// integration from ADR 0004) are rejected with a clear "not yet available"
/// message rather than faking a confirmation for something that touches
/// real money; the Split enum value stays rejected.
/// Split tender: send <c>Parts</c> (one entry per tender, summing exactly to the total). When Parts is present,
/// Method/AmountTendered/CustomerCreditLedgerId on the request itself are ignored and each part carries its own;
/// AllowCreditLimitOverride/CreditLimitOverrideReason still apply to the (at most one) Utang part. Null Parts is the
/// legacy single full payment, unchanged.
/// </summary>
public sealed record RecordPaymentRequest(
    PaymentMethod Method,
    decimal? AmountTendered,
    Guid? CustomerCreditLedgerId = null,
    bool AllowCreditLimitOverride = false,
    string? CreditLimitOverrideReason = null,
    decimal? ExpectedTotal = null,
    IReadOnlyList<PaymentPartRequest>? Parts = null,
    Guid? PaymentKey = null);
