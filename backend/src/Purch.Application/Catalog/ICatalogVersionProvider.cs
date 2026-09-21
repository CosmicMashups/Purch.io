namespace Purch.Application.Catalog;

/// <summary>A cheap validator for the catalog lists: it changes whenever anything a list response is built from
/// changes, and costs a few aggregate queries instead of loading and serialising the whole list. Backs
/// ETag / If-None-Match on GET /items and GET /categories.</summary>
public interface ICatalogVersionProvider
{
    /// <summary>Everything an item list row depends on: items, ingredient stock, recipes and the
    /// separate-inventory-tracking setting.</summary>
    Task<string> GetItemsVersionAsync(CancellationToken cancellationToken = default);

    Task<string> GetCategoriesVersionAsync(CancellationToken cancellationToken = default);
}
