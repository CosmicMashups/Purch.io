using Purch.Application.Catalog;
using Purch.Application.Common;
using Purch.Application.Inventory;
using Purch.Application.Reporting;
using Purch.Domain.Entities;

namespace Purch.UnitTests.Reporting;

public sealed class InventoryValuationServiceTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    private static Item NewItem(string name, decimal stock, decimal? cost, Guid? categoryId = null, bool active = true, bool deleted = false) =>
        new() { Id = Guid.NewGuid(), TenantId = Tenant, Name = name, StockOnHand = stock, CostPrice = cost, CategoryId = categoryId, IsActive = active, IsDeleted = deleted };

    private static InventoryItem NewIngredient(string name, decimal qty, decimal? cost, bool auto = false) =>
        new() { Id = Guid.NewGuid(), TenantId = Tenant, Name = name, QuantityOnHand = qty, UnitCost = cost, IsAutoCreatedForItem = auto };

    private static InventoryValuationService Service(List<Item> items, List<InventoryItem>? ingredients = null, List<Category>? categories = null) =>
        new(new FakeItems(items), new FakeStock(), new FakeIngredients(ingredients ?? []), new FakeCategories(categories ?? []), new FakeInventoryCategories(), new FakeTenant());

    [Fact]
    public async Task Null_cost_is_excluded_from_the_total_and_counted()
    {
        var result = await Service([NewItem("A", 10, 2m), NewItem("B", 5, null)]).GetAsync(new InventoryValuationQuery(null, "all"));

        Assert.Equal(20m, result.TotalValue);
        Assert.Equal(1, result.ValuedCount);
        Assert.Equal(1, result.UnknownCostCount);
        Assert.Null(result.Groups.SelectMany(g => g.Rows).Single(r => r.Name == "B").Value);
    }

    [Fact]
    public async Task Negative_stock_is_valued_zero_and_flagged()
    {
        var result = await Service([NewItem("A", -3, 4m)]).GetAsync(new InventoryValuationQuery(null, "item"));

        var row = result.Groups.Single().Rows.Single();
        Assert.Equal(0m, row.Value);
        Assert.True(row.Flagged);
        Assert.Equal(1, result.NegativeStockCount);
        Assert.Equal(0m, result.TotalValue);
    }

    [Fact]
    public async Task Inactive_is_included_and_soft_deleted_is_excluded()
    {
        var result = await Service([NewItem("Off", 1, 1m, active: false), NewItem("Gone", 1, 1m, deleted: true)]).GetAsync(new InventoryValuationQuery(null, "item"));

        Assert.Equal(["Off"], result.Groups.SelectMany(g => g.Rows).Select(r => r.Name).ToList());
    }

    [Fact]
    public async Task Items_use_cost_price_and_ingredients_use_unit_cost()
    {
        var result = await Service([NewItem("Item", 2, 10m)], [NewIngredient("Flour", 4, 3m), NewIngredient("Linked", 9, 99m, auto: true)])
            .GetAsync(new InventoryValuationQuery(null, "all"));

        var rows = result.Groups.SelectMany(g => g.Rows).ToList();
        Assert.Equal(20m, rows.Single(r => r.Kind == "item").Value);
        Assert.Equal(12m, rows.Single(r => r.Kind == "ingredient").Value);
        Assert.Equal(2, rows.Count);
        Assert.Equal(32m, result.TotalValue);
    }

    [Fact]
    public async Task Group_subtotal_is_the_sum_of_row_values()
    {
        var category = new Category { Id = Guid.NewGuid(), TenantId = Tenant, Name = "Drinks" };
        var result = await Service([NewItem("A", 2, 5m, category.Id), NewItem("B", 1, 7m, category.Id), NewItem("C", 1, 1m)], null, [category])
            .GetAsync(new InventoryValuationQuery(null, "item"));

        var drinks = result.Groups.Single(g => g.Name == "Drinks");
        Assert.Equal(17m, drinks.Subtotal);
        Assert.Equal(drinks.Rows.Sum(r => r.Value), drinks.Subtotal);
        Assert.Equal(18m, result.TotalValue);
    }

    private sealed class FakeTenant : ICurrentTenantProvider
    {
        public Guid? TenantId => Tenant;
    }

    private sealed class FakeStock : IItemStockService
    {
        public Task<Guid?> AdjustAsync(Item item, decimal delta, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyDictionary<Guid, decimal>> GetOnHandAsync(IReadOnlyCollection<Item> items, Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, decimal>>(items.ToDictionary(i => i.Id, i => i.StockOnHand));
    }

    private sealed class FakeItems(List<Item> items) : IItemRepository
    {
        public Task<Item?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<Item>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<Item>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Item>>(items);

        public Task<bool> BarcodeExistsAsync(Guid tenantId, string barcode, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<string?> GetDeletedNameByBarcodeAsync(Guid tenantId, string barcode, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public void Add(Item item) => throw new NotSupportedException();
    }

    private sealed class FakeIngredients(List<InventoryItem> items) : IInventoryItemRepository
    {
        public Task<IReadOnlyList<InventoryItem>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<InventoryItem>>(items);

        public Task<InventoryItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<InventoryItem>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<InventoryItem?> GetByLinkedItemIdAsync(Guid tenantId, Guid itemId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<InventoryItem>> ListByCategoryTrackedAsync(Guid categoryId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public void Add(InventoryItem inventoryItem) => throw new NotSupportedException();
    }

    private sealed class FakeCategories(List<Category> categories) : ICategoryRepository
    {
        public Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<Category>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Category>>(categories);

        public void Add(Category category) => throw new NotSupportedException();
    }

    private sealed class FakeInventoryCategories : IInventoryCategoryRepository
    {
        public Task<InventoryCategory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<InventoryCategory>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<InventoryCategory>>([]);

        public void Add(InventoryCategory category) => throw new NotSupportedException();

        public void Remove(InventoryCategory category) => throw new NotSupportedException();
    }
}
