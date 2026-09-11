namespace Purch.Application.Pos;

/// <summary>
/// Apply=true toggles the 20% RA 9994/RA 10754 Senior Citizen/PWD discount on
/// for the current cart. This is a cashier-facing toggle, not an ID-scanning
/// feature — the cashier verifies the physical ID themselves before tapping
/// it, the same staff-assist model as every other manually-confirmed action
/// in this POS. The 20%-of-subtotal computation is a pragmatic best-effort:
/// full BIR VAT-exemption treatment (the discount is computed off the
/// VAT-exclusive price, not gross) needs accountant/BIR review before this
/// is treated as accreditation-ready — same caveat as ADR 0005's Z/X-reading
/// format.
/// </summary>
public sealed record ApplySeniorPwdDiscountRequest(bool Apply);
