using Purch.Domain.Enums;

namespace Purch.Application.Onboarding;

public sealed record AuditLogDto(
    Guid Id,
    Guid ActorUserId,
    AuditActionType ActionType,
    string TargetEntityType,
    Guid TargetEntityId,
    DateTimeOffset CreatedAt);
