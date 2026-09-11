namespace Purch.Application.Catalog;

public sealed record UpdateItemRequest(
    string Name,
    string? Sku,
    string? Barcode,
    Guid? CategoryId,
    decimal BasePrice,
    string? ImageUrl,
    bool IsActive,
    Guid? DepartmentId = null);
