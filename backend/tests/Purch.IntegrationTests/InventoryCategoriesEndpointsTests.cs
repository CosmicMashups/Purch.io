using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Purch.Application.Catalog;
using Purch.Application.Inventory;
using Purch.Application.Onboarding;
using Purch.Common.TestUtilities;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class InventoryCategoriesEndpointsTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task An_ingredient_can_be_filed_under_a_category_and_the_category_removed_leaves_it_uncategorised()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);

        var category = (await (await client.PostAsJsonAsync("/inventory-categories", new CreateInventoryCategoryRequest("Dairy", 1)))
            .Content.ReadFromJsonAsync<InventoryCategoryDto>(JsonOptions))!;
        var ingredient = (await (await client.PostAsJsonAsync(
                "/inventory-items",
                new CreateInventoryItemRequest("Milk", null, "ml", "carton", 1000m, null, category.Id)))
            .Content.ReadFromJsonAsync<InventoryItemDto>(JsonOptions))!;
        Assert.Equal(category.Id, ingredient.CategoryId);

        var renamed = await client.PutAsJsonAsync($"/inventory-categories/{category.Id}", new UpdateInventoryCategoryRequest("Dairy and eggs", 2));
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);

        var deleted = await client.DeleteAsync($"/inventory-categories/{category.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var ingredients = await client.GetFromJsonAsync<List<InventoryItemDto>>("/inventory-items", JsonOptions);
        Assert.Null(Assert.Single(ingredients!, i => i.Id == ingredient.Id).CategoryId);
        Assert.Empty((await client.GetFromJsonAsync<List<InventoryCategoryDto>>("/inventory-categories", JsonOptions))!);
    }

    [Fact]
    public async Task An_ingredient_cannot_use_a_category_that_does_not_exist()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync(
            "/inventory-items",
            new CreateInventoryItemRequest("Milk", null, "ml", "carton", 1000m, null, Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task The_dashboard_has_no_ingredient_section_until_ingredients_are_tracked_separately()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);

        var off = await client.GetFromJsonAsync<InventoryDashboardDto>("/inventory/dashboard", JsonOptions);
        Assert.Null(off!.Ingredients);

        _ = await client.PutAsJsonAsync("/tenant/settings/inventory-tracking", new UpdateInventoryTrackingSettingRequest(true));
        var on = await client.GetFromJsonAsync<InventoryDashboardDto>("/inventory/dashboard", JsonOptions);
        Assert.NotNull(on!.Ingredients);
    }

    [Fact]
    public async Task The_dashboard_counts_ingredients_apart_from_items_and_skips_the_stock_paired_to_plain_items()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        _ = await client.PutAsJsonAsync("/tenant/settings/inventory-tracking", new UpdateInventoryTrackingSettingRequest(true));

        // A plain item gets an auto-paired stock record; it must not appear among the ingredients.
        _ = await client.PostAsJsonAsync("/items", new CreateItemRequest("Cup", null, null, null, 10m, null, PricingType.Unit));

        var branches = await client.GetFromJsonAsync<List<BranchDto>>("/branches", JsonOptions);
        var branchId = branches!.Single().Id;

        var empty = (await (await client.PostAsJsonAsync("/inventory-items", new CreateInventoryItemRequest("Sugar", null, "g", "g", 1m, 100m)))
            .Content.ReadFromJsonAsync<InventoryItemDto>(JsonOptions))!;
        var low = (await (await client.PostAsJsonAsync("/inventory-items", new CreateInventoryItemRequest("Beans", null, "g", "g", 1m, 100m)))
            .Content.ReadFromJsonAsync<InventoryItemDto>(JsonOptions))!;
        var healthy = (await (await client.PostAsJsonAsync("/inventory-items", new CreateInventoryItemRequest("Flour", null, "g", "g", 1m, 100m)))
            .Content.ReadFromJsonAsync<InventoryItemDto>(JsonOptions))!;
        _ = await client.PostAsJsonAsync($"/inventory-items/{low.Id}/physical-count", new UpdatePhysicalCountRequest(40m, branchId));
        _ = await client.PostAsJsonAsync($"/inventory-items/{healthy.Id}/physical-count", new UpdatePhysicalCountRequest(500m, branchId));

        var dashboard = await client.GetFromJsonAsync<InventoryDashboardDto>("/inventory/dashboard", JsonOptions);
        var ingredients = dashboard!.Ingredients!;

        Assert.Equal(3, ingredients.Total);
        Assert.Equal(1, ingredients.OutOfStockCount);
        Assert.Equal(1, ingredients.LowStockCount);
        Assert.Equal(low.Id, Assert.Single(ingredients.LowStock).InventoryItemId);
        Assert.NotEqual(empty.Id, low.Id);
    }

    [Fact]
    public async Task An_ingredient_no_recipe_deducts_is_flagged_as_counted_by_hand()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        _ = await client.PutAsJsonAsync("/tenant/settings/inventory-tracking", new UpdateInventoryTrackingSettingRequest(true));

        var burger = (await (await client.PostAsJsonAsync("/items", new CreateItemRequest("Burger", null, null, null, 99m, null, PricingType.Unit)))
            .Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;
        var bun = (await (await client.PostAsJsonAsync("/inventory-items", new CreateInventoryItemRequest("Bun", null, "pair", "bag", 10m, null)))
            .Content.ReadFromJsonAsync<InventoryItemDto>(JsonOptions))!;
        var dressing = (await (await client.PostAsJsonAsync("/inventory-items", new CreateInventoryItemRequest("Dressing", null, "mL", "bottle", 500m, null)))
            .Content.ReadFromJsonAsync<InventoryItemDto>(JsonOptions))!;
        var unused = (await (await client.PostAsJsonAsync("/inventory-items", new CreateInventoryItemRequest("Napkins", null, "pc", "pack", 100m, null)))
            .Content.ReadFromJsonAsync<InventoryItemDto>(JsonOptions))!;

        var recipe = await client.PutAsJsonAsync(
            $"/items/{burger.Id}/recipe",
            new ReplaceItemRecipeRequest([new ReplaceItemRecipeLineRequest(bun.Id, 1m), new ReplaceItemRecipeLineRequest(dressing.Id, null)]));
        Assert.Equal(HttpStatusCode.OK, recipe.StatusCode);

        var list = (await client.GetFromJsonAsync<List<InventoryItemDto>>("/inventory-items", JsonOptions))!;
        Assert.False(list.Single(i => i.Id == bun.Id).IsCountedByHand);
        Assert.True(list.Single(i => i.Id == dressing.Id).IsCountedByHand);
        Assert.False(list.Single(i => i.Id == unused.Id).IsCountedByHand);
    }
}
