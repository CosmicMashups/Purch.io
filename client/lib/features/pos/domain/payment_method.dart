/// Sent/received as a plain integer — see onboarding_enums.dart for why.
/// Mirrors Purch.Domain.Enums.PaymentMethod exactly, in the same declared
/// order. Only cash, bankTransfer, and manualGcashQr are wired to a working
/// checkout flow so far — qrPh needs a live Xendit webhook, billPaymentELoad
/// needs the Dragonpay integration from ADR 0004, utangCredit is Phase 9 per
/// the implementation plan, and split (multi-method) isn't built yet.
enum PaymentMethod {
  cash,
  qrPh,
  bankTransfer,
  manualGcashQr,
  billPaymentELoad,
  utangCredit,
  split,
}

/// Mirrors Purch.Domain.Enums.PaymentStatus exactly, in the same declared order.
enum PaymentStatus { pending, confirmed, failed, cancelled }
