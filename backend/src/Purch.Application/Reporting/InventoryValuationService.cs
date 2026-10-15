using Purch.Application.Catalog;
using Purch.Application.Common;
using Purch.Application.Inventory;

namespace Purch.Application.Reporting;

/// <summary>What the stock on hand is worth at cost. Stock is tenant-wide. An unknown cost is never
/// treated as zero: the row is listed, left out of the total and counted.</summary>
public sealed class InventoryValuationService(
    IItemRepository itemRepository,
    IItemStockService itemStockService,
    IInventoryItemRepository inventoryItemRepository,
    ICategoryRepository categoryRepository,
    IInventoryCategoryRepository inventoryCategoryRepository,
    ICurrentTenantProvider currentTenantProvider) : IInventoryValuationService
{
    public async Task<InventoryValuationDto> GetAsync(InventoryValuationQuery query, CancellationToken cancellationToken = default)
    {
        var tenantId = currentTenantProvider.TenantId
            ?? throw new InvalidOperationException("The inventory valuation requires an authenticated tenant context.");
        var type = (query.Type ?? "all").ToLowerInvariant();
        var rows = new List<(Guid? CategoryId, string? CategoryName, ValuationRowDto Row)>();

        if (type is "all" or "item")
        {
            var categories = (await categoryRepository.ListByTenantAsync(tenantId, cancellationToken)).ToDictionary(c => c.Id, c => c.Name);
            var items = (await itemRepository.ListByTenantAsync(tenantId, cancellationToken))
                .Where(item => !item.IsDeleted && (query.CategoryId is null || item.CategoryId == query.CategoryId))
                .ToList();

            // Items that hold no stock of their own (services, combos, recipe items) are not in the map.
            var onHand = await itemStockService.GetOnHandAsync(items, tenantId, cancellationToken);
            foreach (var item in items.Where(item => onHand.ContainsKey(item.Id)))
            {
                var name = item.CategoryId is { } id && categories.TryGetValue(id, out var n) ? n : null;
                rows.Add((item.CategoryId, name, Row(item.Id, "item", item.Name, item.Sku, name, onHand[item.Id], item.CostPrice)));
            }
        }

        if (type is "all" or "ingredient")
        {
            var categories = (await inventoryCategoryRepository.ListByTenantAsync(tenantId, cancellationToken)).ToDictionary(c => c.Id, c => c.Name);
            var ingredients = (await inventoryItemRepository.ListByTenantAsync(tenantId, cancellationToken))
                // An auto-created record is the stock behind a catalog item, already valued as that item.
                .Where(i => !i.IsDeleted && !i.IsAutoCreatedForItem && (query.CategoryId is null || i.CategoryId == query.CategoryId));
            foreach (var ingredient in ingredients)
            {
                var name = ingredient.CategoryId is { } id && categories.TryGetValue(id, out var n) ? n : null;
                rows.Add((ingredient.CategoryId, name, Row(ingredient.Id, "ingredient", ingredient.Name, ingredient.Sku, name, ingredient.QuantityOnHand, ingredient.UnitCost)));
            }
        }

        var groups = rows
            .GroupBy(r => r.CategoryId)
            .Select(g => new ValuationGroupDto(
                g.Key,
                g.First().CategoryName ?? "Uncategorised",
                g.Sum(r => r.Row.Value ?? 0m),
                g.Select(r => r.Row).OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList()))
            .OrderBy(g => g.CategoryId is null ? 1 : 0)
            .ThenBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var all = rows.Select(r => r.Row).ToList();
        return new InventoryValuationDto(
            groups,
            all.Sum(r => r.Value ?? 0m),
            all.Count(r => r.Value is not null),
            all.Count(r => r.UnitCost is null),
            all.Count(r => r.OnHand < 0));
    }

    private static ValuationRowDto Row(Guid id, string kind, string name, string? sku, string? category, decimal onHand, decimal? unitCost)
    {
        // Negative stock is a data problem, not negative worth: valued 0 and flagged.
        decimal? value = unitCost is { } cost ? Math.Max(onHand, 0m) * cost : null;
        return new ValuationRowDto(id, kind, name, sku, category, onHand, unitCost, value, onHand < 0);
    }
}
