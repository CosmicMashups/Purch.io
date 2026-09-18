namespace Purch.Application.Promotions;

public sealed record BogoPromoRuleDto(
    Guid Id,
    string Name,
    Guid TriggerItemId,
    int TriggerQuantity,
    Guid FreeItemId,
    int FreeQuantity,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    bool IsActive);

public sealed record CreateBogoPromoRuleRequest(
    string Name,
    Guid TriggerItemId,
    int TriggerQuantity,
    Guid FreeItemId,
    int FreeQuantity,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt);

public sealed record UpdateBogoPromoRuleRequest(
    string Name,
    Guid TriggerItemId,
    int TriggerQuantity,
    Guid FreeItemId,
    int FreeQuantity,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    bool IsActive);
