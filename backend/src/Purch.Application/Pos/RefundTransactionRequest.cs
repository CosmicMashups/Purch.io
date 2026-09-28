namespace Purch.Application.Pos;

/// <summary>Refunding a completed sale always needs an Admin/Manager approval — see
/// ApproverAuthorizationService — even when an Admin/Manager is the one asking.</summary>
public sealed record RefundTransactionRequest(string Reason, string? ApproverPin = null);
