using Purch.Domain.Enums;

namespace Purch.Application.Onboarding;

/// <summary>All filters optional — A6's audit log viewer is "filterable by staff/date/action type," not required to filter.</summary>
public sealed record AuditLogQuery(
    Guid? ActorUserId,
    AuditActionType? ActionType,
    DateTimeOffset? From,
    DateTimeOffset? To,
    DateTimeOffset? Before = null,
    int? Limit = null);
