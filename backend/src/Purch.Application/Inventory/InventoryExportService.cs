using Purch.Application.Catalog;
using Purch.Application.Common;
using Purch.Application.Inventory.Import;
using Purch.Application.Reporting;
using Purch.Domain.Entities;

namespace Purch.Application.Inventory;

public sealed class InventoryExportService(
    IItemRepository itemRepository,
    IInventoryItemRepository inventoryItemRepository,
    ICategoryRepository categoryRepository,
    IInventoryCategoryRepository inventoryCategoryRepository,
    IItemStockService itemStockService,
    ICurrentTenantProvider currentTenantProvider,
    ICurrentActorProvider currentActorProvider) : IInventoryExportService
{
    private const string Bom = "\uFEFF";

    private Guid TenantId => currentTenantProvider.TenantId ?? throw new InvalidOperationException("No tenant.");

    public async Task<string> ExportItemsAsync(CancellationToken cancellationToken = default)
    {
        var (items, stock) = await LoadItemsAsync(cancellationToken);
        var categories = (await categoryRepository.ListByTenantAsync(TenantId, cancellationToken)).ToDictionary(c => c.Id, c => c.Name);
        var rows = items.Select(item => CatalogRow(
            "item", item.Sku, item.Barcode, item.Name, Lookup(categories, item.CategoryId), item.BasePrice, item.CostPrice,
            item.LowStockThreshold, item.Unit, item.IsActive, item.Id, stock.TryGetValue(item.Id, out var q) ? q : null, item.UpdatedAt));
        return Build(ImportColumns.Catalog, rows, currentActorProvider.CanSeeCosts);
    }

    public async Task<string> ExportIngredientsAsync(CancellationToken cancellationToken = default)
    {
        var categories = (await inventoryCategoryRepository.ListByTenantAsync(TenantId, cancellationToken)).ToDictionary(c => c.Id, c => c.Name);
        var rows = (await LoadIngredientsAsync(cancellationToken)).Select(i => CatalogRow(
            "ingredient", i.Sku, null, i.Name, Lookup(categories, i.CategoryId), null, i.UnitCost,
            i.LowStockThreshold, i.BaseUnit, i.IsActive, i.Id, i.QuantityOnHand, i.UpdatedAt));
        return Build(ImportColumns.Catalog, rows, currentActorProvider.CanSeeCosts);
    }

    public async Task<string> ExportStockAsync(CancellationToken cancellationToken = default)
    {
        var (items, stock) = await LoadItemsAsync(cancellationToken);
        var holding = items.Where(i => stock.ContainsKey(i.Id))
            .Select(i => StockRow("item", i.Sku, i.Barcode, i.Name, stock[i.Id], i.Id, i.UpdatedAt));
        var ingredients = (await LoadIngredientsAsync(cancellationToken))
            .Select(i => StockRow("ingredient", i.Sku, null, i.Name, i.QuantityOnHand, i.Id, i.UpdatedAt));
        return Build(ImportColumns.Stock, holding.Concat(ingredients), true);
    }

    private async Task<(List<Item> Items, IReadOnlyDictionary<Guid, decimal> Stock)> LoadItemsAsync(CancellationToken ct)
    {
        var all = await itemRepository.ListByTenantAsync(TenantId, ct);
        var items = all.Where(i => !i.IsDeleted).OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToList();
        return (items, await itemStockService.GetOnHandAsync(items, TenantId, ct));
    }

    private async Task<List<InventoryItem>> LoadIngredientsAsync(CancellationToken ct) =>
        (await inventoryItemRepository.ListByTenantAsync(TenantId, ct))
            .Where(i => !i.IsDeleted && i.LinkedItemId is null && !i.IsAutoCreatedForItem)
            .OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToList();

    private static string Lookup(Dictionary<Guid, string> names, Guid? id) => id is { } key && names.TryGetValue(key, out var n) ? n : "";

    private static string Num(decimal? value) => value is { } v ? ReportCsv.Money(v) : "";

    // Barcodes are emitted as raw digits (no leading apostrophe, which would change the value). Spreadsheet apps may
    // strip leading zeros when the file is opened; import compares barcodes as text.
    private static Dictionary<string, string> CatalogRow(
        string type, string? sku, string? barcode, string name, string category, decimal? price, decimal? cost,
        decimal? reorder, string? unit, bool active, Guid id, decimal? stock, DateTimeOffset? updatedAt) => new()
        {
            ["type"] = type,
            ["sku"] = ReportCsv.Field(sku ?? ""),
            ["barcode"] = ReportCsv.Field(barcode ?? ""),
            ["name"] = ReportCsv.Field(name),
            ["category"] = ReportCsv.Field(category),
            ["price"] = Num(price),
            ["cost"] = Num(cost),
            ["reorder_threshold"] = Num(reorder),
            ["unit"] = ReportCsv.Field(unit ?? ""),
            ["active"] = active ? "true" : "false",
            ["id"] = id.ToString(),
            ["current_stock"] = Num(stock),
            ["updated_at"] = updatedAt is { } t ? ReportCsv.Time(t) : "",
        };

    private static Dictionary<string, string> StockRow(string type, string? sku, string? barcode, string name, decimal stock, Guid id, DateTimeOffset? updatedAt) => new()
    {
        ["type"] = type,
        ["sku"] = ReportCsv.Field(sku ?? ""),
        ["barcode"] = ReportCsv.Field(barcode ?? ""),
        ["name"] = ReportCsv.Field(name),
        ["counted_qty"] = Num(stock), // pre-filled so re-importing an untouched export is a zero-delta no-op
        ["note"] = "",
        ["id"] = id.ToString(),
        ["current_stock"] = Num(stock),
        ["updated_at"] = updatedAt is { } t ? ReportCsv.Time(t) : "",
    };

    private static string Build(string[] columns, IEnumerable<Dictionary<string, string>> rows, bool includeCost)
    {
        var headers = columns.Concat(ImportColumns.ReadOnly).Where(c => includeCost || c != "cost").ToArray();
        return Bom + ReportCsv.Build(string.Join(',', headers), rows.Select(r => headers.Select(h => r[h])));
    }
}
