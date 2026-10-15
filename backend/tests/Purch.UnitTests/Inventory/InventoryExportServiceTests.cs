using Purch.Application.Catalog;
using Purch.Application.Common;
using Purch.Application.Inventory;
using Purch.Application.Inventory.Import;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.UnitTests.Inventory;

public sealed class InventoryExportServiceTests
{
    private static readonly Guid TenantGuid = Guid.NewGuid();
    private readonly List<Item> items = [];
    private readonly List<InventoryItem> ingredients = [];
    private readonly Actor actor = new();

    private InventoryExportService Service() => new(
        new Items(items), new Ingredients(ingredients), new Categories(), new InvCategories(), new Stock(), new Tenant(), actor);

    private Item Add(string name, decimal stock = 0m, bool deleted = false, bool active = true)
    {
        var item = new Item { TenantId = TenantGuid, Name = name, StockOnHand = stock, CostPrice = 3m, BasePrice = 5m, IsDeleted = deleted, IsActive = active, Barcode = "00123" };
        items.Add(item);
        return item;
    }

    private static string[] HeaderOf(string csv) => csv.TrimStart('﻿').Split('\n')[0].TrimEnd('\r').Split(',');

    [Fact]
    public async Task Has_bom_and_header_equals_import_columns_then_read_only()
    {
        _ = Add("Rice");
        var csv = await Service().ExportItemsAsync();

        Assert.StartsWith("﻿", csv);
        Assert.Equal(ImportColumns.Catalog.Concat(ImportColumns.ReadOnly), HeaderOf(csv));
    }

    [Fact]
    public async Task Formula_leading_cell_is_neutralised()
    {
        _ = Add("=SUM(A1)");
        Assert.Contains("'=SUM(A1)", await Service().ExportItemsAsync());
    }

    [Fact]
    public async Task Cost_column_is_absent_without_cost_visibility_but_price_stays()
    {
        _ = Add("Rice");
        actor.CurrentRole = Role.Cashier;
        var header = HeaderOf(await Service().ExportItemsAsync());

        Assert.DoesNotContain("cost", header);
        Assert.Contains("price", header);
    }

    [Fact]
    public async Task Inactive_is_included_as_false_and_soft_deleted_is_excluded()
    {
        _ = Add("Dormant", active: false);
        _ = Add("Gone", deleted: true);
        var csv = await Service().ExportItemsAsync();

        Assert.Contains("Dormant", csv);
        Assert.Contains(",false,", csv);
        Assert.DoesNotContain("Gone", csv);
    }

    [Fact]
    public async Task Stock_csv_prefills_counted_qty_with_current_stock_and_keeps_barcode_digits()
    {
        _ = Add("Rice", 12.5m);
        ingredients.Add(new InventoryItem { TenantId = TenantGuid, Name = "Flour", QuantityOnHand = 4m });
        var csv = await Service().ExportStockAsync();
        var lines = csv.TrimStart('﻿').Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r')).ToList();
        var header = HeaderOf(csv);
        var counted = Array.IndexOf(header, "counted_qty");
        var current = Array.IndexOf(header, "current_stock");

        Assert.Equal(ImportColumns.Stock.Concat(ImportColumns.ReadOnly), header);
        Assert.Equal(3, lines.Count);
        foreach (var row in lines.Skip(1).Select(l => l.Split(',')))
        {
            Assert.Equal(row[current], row[counted]);
        }

        Assert.Contains("00123", csv);
    }

    private sealed class Tenant : ICurrentTenantProvider
    {
        public Guid? TenantId => TenantGuid;
    }

    private sealed class Actor : ICurrentActorProvider
    {
        public Role CurrentRole { get; set; } = Domain.Enums.Role.Manager;
        public Guid? UserId => null;
        public Guid? DeviceId => null;
        public Guid? BranchId => null;
        public ScopeType? ScopeType => null;
        public Guid? ScopeId => null;
        public Role? Role => CurrentRole;
    }

    private sealed class Stock : IItemStockService
    {
        public Task<Guid?> AdjustAsync(Item item, decimal delta, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyDictionary<Guid, decimal>> GetOnHandAsync(IReadOnlyCollection<Item> items, Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, decimal>>(items.ToDictionary(i => i.Id, i => i.StockOnHand));
    }

    private sealed class Items(List<Item> list) : IItemRepository
    {
        public Task<Item?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Item>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Item>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Item>>(list);
        public Task<bool> BarcodeExistsAsync(Guid tenantId, string barcode, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string?> GetDeletedNameByBarcodeAsync(Guid tenantId, string barcode, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Add(Item item) => throw new NotSupportedException();
    }

    private sealed class Ingredients(List<InventoryItem> list) : IInventoryItemRepository
    {
        public Task<IReadOnlyList<InventoryItem>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<InventoryItem>>(list);
        public Task<InventoryItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<InventoryItem>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<InventoryItem?> GetByLinkedItemIdAsync(Guid tenantId, Guid itemId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<InventoryItem>> ListByCategoryTrackedAsync(Guid categoryId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Add(InventoryItem inventoryItem) => throw new NotSupportedException();
    }

    private sealed class Categories : ICategoryRepository
    {
        public Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Category>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Category>>([]);
        public void Add(Category category) => throw new NotSupportedException();
    }

    private sealed class InvCategories : IInventoryCategoryRepository
    {
        public Task<InventoryCategory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<InventoryCategory>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<InventoryCategory>>([]);
        public void Add(InventoryCategory category) => throw new NotSupportedException();
        public void Remove(InventoryCategory category) => throw new NotSupportedException();
    }
}
