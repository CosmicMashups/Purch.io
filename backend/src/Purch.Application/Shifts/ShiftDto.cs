using Purch.Domain.Enums;

namespace Purch.Application.Shifts;

public sealed record ShiftDto(
    Guid Id,
    Guid BranchId,
    Guid DeviceId,
    ShiftStatus Status,
    Guid OpenedByUserId,
    string OpenedByUserName,
    decimal OpeningCashAmount,
    DateTimeOffset OpenedAt,
    Guid? ClosedByUserId,
    string? ClosedByUserName,
    decimal? ClosingCashAmount,
    decimal? ExpectedCashAmount,
    decimal? VarianceAmount,
    string? HandoverNotes,
    Guid? ApprovedByUserId,
    string? ApprovedByUserName,
    DateTimeOffset? ClosedAt);
