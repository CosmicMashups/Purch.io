namespace Purch.Application.Shifts;

public sealed record OpenShiftRequest(decimal OpeningCashAmount);

/// <summary>ApproverPin is required only when the cash count comes up short or
/// over — a fresh PIN check against an active Admin/Manager, the same trust
/// model as every other manager-override action in this POS, rather than
/// trusting a client-picked approver name.</summary>
public sealed record CloseShiftRequest(decimal ClosingCashAmount, string? HandoverNotes, string? ApproverPin);
