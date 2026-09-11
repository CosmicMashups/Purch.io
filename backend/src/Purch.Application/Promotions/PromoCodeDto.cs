using Purch.Domain.Enums;

namespace Purch.Application.Promotions;

public sealed record PromoCodeDto(
    Guid Id,
    string Code,
    PromoDiscountType DiscountType,
    decimal DiscountValue,
    bool IsActive,
    DateTimeOffset? ExpiresAt);

public sealed record CreatePromoCodeRequest(
    string Code,
    PromoDiscountType DiscountType,
    decimal DiscountValue,
    DateTimeOffset? ExpiresAt);
