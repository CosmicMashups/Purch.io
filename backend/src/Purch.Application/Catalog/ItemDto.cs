using Purch.Domain.Enums;

namespace Purch.Application.Catalog;

public sealed record ItemDto(
    Guid Id,
    string Name,
    string? Sku,
    string? Barcode,
    Guid? CategoryId,
    decimal BasePrice,
    string? ImageUrl,
    PricingType PricingType,
    decimal StockOnHand,
    bool IsActive,
    TingiMode TingiMode,
    decimal? PackagedSize,
    decimal? TingiIncrementStep,
    IReadOnlyList<decimal> TingiAllowedSizes,
    int? ServiceDurationMinutes,
    Guid? DepartmentId);
