using Purch.Domain.Enums;

namespace Purch.Application.Approvals;

/// <summary>One void, refund or kitchen-order-edit approval, as the review needs it — no longer tied to the
/// raw audit entry's JSON blob.</summary>
public sealed record ApprovalEntryDto(
    Guid RequesterId,
    string RequesterName,
    AuditActionType ActionType,
    Guid TargetEntityId,
    DateTimeOffset CreatedAt,
    bool AfterHours);

/// <summary>Every approval one Admin/Manager signed off in the window, with the patterns worth a second
/// look already called out — see ApprovalsReviewCalculator for what "unusual" means here.</summary>
public sealed record ApproverSummaryDto(
    Guid ApproverId,
    string ApproverName,
    Role ApproverRole,
    int TotalApprovals,
    int AfterHoursApprovals,
    IReadOnlyList<ApprovalFlagDto> Flags,
    IReadOnlyList<ApprovalEntryDto> Entries);

/// <summary>A single called-out pattern, in the reader's own words — never just a code the manager has to
/// look up.</summary>
public sealed record ApprovalFlagDto(string Message);

public sealed record ApprovalsReviewDto(
    DateOnly Date,
    int TotalApprovals,
    IReadOnlyList<ApproverSummaryDto> Approvers);
