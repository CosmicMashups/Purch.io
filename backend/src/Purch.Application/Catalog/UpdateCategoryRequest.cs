namespace Purch.Application.Catalog;

public sealed record UpdateCategoryRequest(string Name, int SortOrder, string? ImageUrl = null);
