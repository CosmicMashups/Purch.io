using Purch.Domain.Enums;

namespace Purch.Application.Promotions;

public sealed record ItemDiscountPromoRuleDto(
    Guid Id,
    string Name,
    Guid ItemId,
    PromoDiscountType DiscountType,
    decimal DiscountValue,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    bool IsActive);

public sealed record CreateItemDiscountPromoRuleRequest(
    string Name,
    Guid ItemId,
    PromoDiscountType DiscountType,
    decimal DiscountValue,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt);

public sealed record UpdateItemDiscountPromoRuleRequest(
    string Name,
    Guid ItemId,
    PromoDiscountType DiscountType,
    decimal DiscountValue,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    bool IsActive);
