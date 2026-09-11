using Purch.Domain.Enums;

namespace Purch.Application.Catalog;

/// <summary>
/// PricingType is set once at creation and never changed afterward — switching
/// a live item between e.g. Unit and VariantMatrix would orphan whatever
/// sub-resources (variants, combo slots, batches) it had accumulated.
/// </summary>
public sealed record CreateItemRequest(
    string Name,
    string? Sku,
    string? Barcode,
    Guid? CategoryId,
    decimal BasePrice,
    string? ImageUrl,
    PricingType PricingType,
    Guid? DepartmentId = null);
