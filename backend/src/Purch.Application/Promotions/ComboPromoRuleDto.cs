namespace Purch.Application.Promotions;

public sealed record ComboPromoRuleDto(
    Guid Id,
    string Name,
    Guid ItemAId,
    Guid ItemBId,
    decimal ComboPrice,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    bool IsActive);

public sealed record CreateComboPromoRuleRequest(
    string Name,
    Guid ItemAId,
    Guid ItemBId,
    decimal ComboPrice,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt);

public sealed record UpdateComboPromoRuleRequest(
    string Name,
    Guid ItemAId,
    Guid ItemBId,
    decimal ComboPrice,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    bool IsActive);
