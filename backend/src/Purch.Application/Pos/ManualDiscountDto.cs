namespace Purch.Application.Pos;

/// <summary>Every call needs an Admin/Manager ApproverPin (another account's, unless the tenant has only one) (400 field approverPin when missing). Kind is "Percent" or "Fixed" (case-insensitive). A Value of 0 clears the discount. Reason is required.</summary>
public sealed record SetManualDiscountRequest(string Kind, decimal Value, string Reason, string? ApproverPin = null);

/// <summary>Replaces a line's unit price for this sale. The line's PriceOverrideFrom keeps the FIRST (catalog) price the line
/// was overridden from, even across repeated overrides; the audit row's before/after carries the true prior price.</summary>
public sealed record OverrideLinePriceRequest(decimal NewUnitPrice, string Reason, string? ApproverPin = null);
