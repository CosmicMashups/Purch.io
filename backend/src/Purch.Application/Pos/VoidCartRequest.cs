namespace Purch.Application.Pos;

/// <summary>Voiding a cart with items in it always needs an Admin/Manager approval — see
/// ApproverAuthorizationService — even when an Admin/Manager is the one asking; an empty cart needs none,
/// since there's nothing to lose. <see cref="Reason"/> is recorded on the audit entry.</summary>
public sealed record VoidCartRequest(string? ApproverPin, string? Reason = null);
