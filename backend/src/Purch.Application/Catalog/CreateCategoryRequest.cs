namespace Purch.Application.Catalog;

public sealed record CreateCategoryRequest(string Name, int SortOrder, string? ImageUrl = null);
