namespace Purch.Application.Pos;

/// <summary>
/// Apply=true toggles the 20% RA 9994/RA 10754 Senior Citizen/PWD discount on
/// for the current cart. This is a cashier-facing toggle, not an ID-scanning
/// feature — the cashier verifies the physical ID themselves before tapping
/// it, the same staff-assist model as every other manually-confirmed action
/// in this POS. Turning it on is the cashier choosing the statutory discount
/// INSTEAD of promotions: under RA 9994 it cannot be combined with a promo
/// code or any promotional discount, so while it is on every promotion on the
/// cart is suppressed (see TransactionService.RecalculateTotalAsync), and it
/// is taken off the regular, pre-promo subtotal.
/// Prices include 12% VAT. The sale is VAT-exempt (the VAT comes off first,
/// stored as Transaction.VatExemptAmount) and the 20% is then taken off the
/// VAT-exclusive price. Still best-effort until an accountant/BIR reviews it —
/// same caveat as ADR 0005's Z/X-reading format.
/// </summary>
public sealed record ApplySeniorPwdDiscountRequest(bool Apply);
