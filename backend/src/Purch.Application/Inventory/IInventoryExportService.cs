namespace Purch.Application.Inventory;

/// <summary>Each method returns the CSV text (UTF-8 BOM first) for tenant-wide items, ingredients or a stock-count sheet.</summary>
public interface IInventoryExportService
{
    Task<string> ExportItemsAsync(CancellationToken cancellationToken = default);

    Task<string> ExportIngredientsAsync(CancellationToken cancellationToken = default);

    Task<string> ExportStockAsync(CancellationToken cancellationToken = default);
}
