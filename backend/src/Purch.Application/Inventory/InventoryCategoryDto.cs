namespace Purch.Application.Inventory;

public sealed record InventoryCategoryDto(Guid Id, string Name, int SortOrder);

public sealed record CreateInventoryCategoryRequest(string Name, int SortOrder);

public sealed record UpdateInventoryCategoryRequest(string Name, int SortOrder);
