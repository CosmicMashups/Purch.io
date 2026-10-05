namespace Purch.Application.Catalog;

public sealed record UpdateItemRequest(
    string Name,
    string? Sku,
    string? Barcode,
    Guid? CategoryId,
    decimal BasePrice,
    string? ImageUrl,
    bool IsActive,
    Guid? DepartmentId = null,
    int? SortOrder = null);

/// <summary>The items in their new order; each one's position in the list becomes its sort order.</summary>
public sealed record ReorderItemsRequest(IReadOnlyList<Guid> ItemIds);
