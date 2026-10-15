using Purch.Application.Catalog;
using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Application.Reporting;
using Purch.Domain.Entities;

namespace Purch.UnitTests.Reporting;

public sealed class InventoryCogsServiceTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly DateTimeOffset Noon = new(2026, 3, 10, 4, 0, 0, TimeSpan.Zero);

    private static CogsLineRow Line(Guid? item, decimal qty, decimal total, decimal? cost, decimal returned = 0m, bool open = false, Guid? category = null) =>
        new(Noon, item, category, open, qty, returned, total, cost);

    private static async Task<InventoryCogsDto> RunAsync(List<CogsLineRow> lines, string groupBy = "item", List<Category>? categories = null) =>
        await new InventoryCogsService(new FakeRepo(lines), new FakeItems(), new FakeCategories(categories ?? []), new FakeTenant())
            .GetAsync(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31), groupBy);

    [Fact]
    public async Task Cogs_is_quantity_times_unit_cost_summed()
    {
        var a = Guid.NewGuid();
        var result = await RunAsync([Line(a, 2, 20m, 3m), Line(a, 4, 40m, 2.5m)]);

        Assert.Equal(16m, result.Cogs);
        Assert.Equal(60m, result.KnownCostRevenue);
        Assert.Equal(100m, result.CoveragePercent);
        Assert.Equal(0, result.ExcludedLineCount);
    }

    [Fact]
    public async Task Null_cost_line_is_excluded_counted_and_lowers_coverage()
    {
        var result = await RunAsync([Line(Guid.NewGuid(), 1, 75m, 10m), Line(Guid.NewGuid(), 1, 25m, null)]);

        Assert.Equal(10m, result.Cogs);
        Assert.Equal(75m, result.KnownCostRevenue);
        Assert.Equal(75m, result.CoveragePercent);
        Assert.Equal(1, result.ExcludedLineCount);
    }

    [Fact]
    public async Task Refunded_quantity_is_subtracted()
    {
        var a = Guid.NewGuid();
        var result = await RunAsync([Line(a, 4, 40m, 5m, returned: 1m), Line(a, 2, 20m, 5m, returned: 2m)]);

        Assert.Equal(15m, result.Cogs);
        Assert.Equal(30m, result.KnownCostRevenue);
        Assert.Equal(3m, result.Groups.Single().Quantity);
    }

    [Fact]
    public async Task Open_items_are_excluded_and_counted()
    {
        var result = await RunAsync([Line(null, 1, 50m, null, open: true), Line(Guid.NewGuid(), 1, 50m, 10m)]);

        Assert.Equal(10m, result.Cogs);
        Assert.Equal(1, result.ExcludedLineCount);
        Assert.Equal(50m, result.CoveragePercent);
    }

    [Fact]
    public async Task Groups_by_category_and_day()
    {
        var cat = new Category { Id = Guid.NewGuid(), TenantId = Tenant, Name = "Drinks" };
        var lines = new List<CogsLineRow> { Line(Guid.NewGuid(), 1, 10m, 4m, category: cat.Id), Line(Guid.NewGuid(), 1, 10m, 1m) };

        var byCategory = await RunAsync(lines, "category", [cat]);
        Assert.Equal(["Drinks", "Uncategorised"], byCategory.Groups.Select(g => g.Name).ToList());

        var byDay = await RunAsync(lines, "day");
        Assert.Equal("2026-03-10", byDay.Groups.Single().Name);
        Assert.Equal(5m, byDay.Groups.Single().Cogs);
    }

    [Fact]
    public async Task Range_over_366_days_is_rejected()
    {
        var service = new InventoryCogsService(new FakeRepo([]), new FakeItems(), new FakeCategories([]), new FakeTenant());
        _ = await Assert.ThrowsAsync<ValidationException>(() => service.GetAsync(new DateOnly(2025, 1, 1), new DateOnly(2026, 3, 1), "item"));
    }

    private sealed class FakeTenant : ICurrentTenantProvider
    {
        public Guid? TenantId => Tenant;
    }

    private sealed class FakeRepo(List<CogsLineRow> lines) : IInventoryCogsRepository
    {
        public Task<IReadOnlyList<CogsLineRow>> ListLinesAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CogsLineRow>>(lines);
    }

    private sealed class FakeItems : IItemRepository
    {
        public Task<Item?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<Item>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<Item>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Item>>([]);

        public Task<bool> BarcodeExistsAsync(Guid tenantId, string barcode, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<string?> GetDeletedNameByBarcodeAsync(Guid tenantId, string barcode, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public void Add(Item item) => throw new NotSupportedException();
    }

    private sealed class FakeCategories(List<Category> categories) : ICategoryRepository
    {
        public Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<Category>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Category>>(categories);

        public void Add(Category category) => throw new NotSupportedException();
    }
}
