using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Purch.Application.Catalog;
using Purch.Application.Inventory;
using Purch.Application.Onboarding;
using Purch.Application.Pos;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests;

/// <summary>A modifier can list the inventory it uses up (Coke Zero takes syrup and carbonated water), which
/// is deducted when it is sold and decides whether the option can be chosen at all.</summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class ModifierIngredientTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed class Shop(HttpClient client, Guid branchId) : IDisposable
    {
        public HttpClient Client { get; } = client;

        public Guid BranchId { get; } = branchId;

        public void Dispose()
        {
            Client.Dispose();
        }

        public async Task<InventoryItemDto> IngredientAsync(string name, decimal onHand)
        {
            var created = (await (await Client.PostAsJsonAsync("/inventory-items", new CreateInventoryItemRequest(name, null, "g", "g", 1m, null))).Content.ReadFromJsonAsync<InventoryItemDto>(JsonOptions))!;
            return await SetOnHandAsync(created.Id, onHand);
        }

        public async Task<InventoryItemDto> SetOnHandAsync(Guid inventoryItemId, decimal quantity)
        {
            var response = await Client.PostAsJsonAsync($"/inventory-items/{inventoryItemId}/physical-count", new UpdatePhysicalCountRequest(quantity, BranchId));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<InventoryItemDto>(JsonOptions))!;
        }

        public async Task<decimal> OnHandAsync(Guid inventoryItemId)
        {
            return (await Client.GetFromJsonAsync<List<InventoryItemDto>>("/inventory-items", JsonOptions))!.Single(i => i.Id == inventoryItemId).QuantityOnHand;
        }

        /// <summary>A plain item that has a "Drinks" group with a Coke Zero option attached, and a Coke Zero that
        /// uses the given ingredients.</summary>
        public async Task<(ItemDto Meal, ItemModifierDto CokeZero)> MealWithCokeZeroAsync(params (InventoryItemDto Ingredient, decimal? PerSelection)[] cokeIngredients)
        {
            var meal = (await (await Client.PostAsJsonAsync("/items", new CreateItemRequest("Chicken Meal", null, null, null, 99m, null, PricingType.Unit))).Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;
            var group = (await (await Client.PostAsJsonAsync("/modifier-groups", new CreateModifierGroupRequest("Add drinks", false, false))).Content.ReadFromJsonAsync<ModifierGroupDto>(JsonOptions))!;
            var withModifier = (await (await Client.PostAsJsonAsync($"/modifier-groups/{group.Id}/modifiers", new CreateItemModifierRequest("Coke Zero", 25m))).Content.ReadFromJsonAsync<ModifierGroupDto>(JsonOptions))!;
            var cokeZero = withModifier.Modifiers.Single();
            _ = await Client.PostAsJsonAsync($"/items/{meal.Id}/modifier-groups", new AttachModifierGroupRequest(group.Id));

            var response = await Client.PutAsJsonAsync(
                $"/modifiers/{cokeZero.Id}/ingredients",
                new ReplaceModifierIngredientsRequest([.. cokeIngredients.Select(line => new ReplaceModifierIngredientLine(line.Ingredient.Id, line.PerSelection))]));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            return (meal, cokeZero);
        }

        public async Task<ItemModifierDto> CokeZeroAsync(Guid mealId)
        {
            var groups = (await Client.GetFromJsonAsync<List<ModifierGroupDto>>($"/items/{mealId}/modifier-groups", JsonOptions))!;
            return groups.Single().Modifiers.Single();
        }

        public Task<HttpResponseMessage> AddAsync(ItemDto meal, ItemModifierDto modifier, decimal quantity)
        {
            return Client.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(meal.Id, null, quantity, null, [modifier.Id]));
        }

        public async Task PayAsync()
        {
            var pay = await Client.PostAsJsonAsync("/transactions/cart/payments", new RecordPaymentRequest(PaymentMethod.Cash, 100000m));
            Assert.Equal(HttpStatusCode.OK, pay.StatusCode);
        }
    }

    private static async Task<Shop> OpenShopAsync(PurchApiFactory factory, bool separateTracking = true)
    {
        var client = await TestSessions.AdminClientAsync(factory);
        if (separateTracking)
        {
            var toggle = await client.PutAsJsonAsync("/tenant/settings/inventory-tracking", new UpdateInventoryTrackingSettingRequest(true));
            Assert.Equal(HttpStatusCode.OK, toggle.StatusCode);
        }

        var branchId = (await client.GetFromJsonAsync<List<BranchDto>>("/branches", JsonOptions))!.Single().Id;
        return new Shop(client, branchId);
    }

    [Fact]
    public async Task Selling_a_modifier_uses_up_its_ingredients_times_the_quantity_sold()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var shop = await OpenShopAsync(factory);
        var syrup = await shop.IngredientAsync("Coke Zero Syrup", 1000m);
        var water = await shop.IngredientAsync("Carbonated Water", 5000m);
        var (meal, cokeZero) = await shop.MealWithCokeZeroAsync((syrup, 30m), (water, 250m));

        Assert.Equal(HttpStatusCode.OK, (await shop.AddAsync(meal, cokeZero, 2m)).StatusCode);
        await shop.PayAsync();

        Assert.Equal(1000m - (2 * 30m), await shop.OnHandAsync(syrup.Id));
        Assert.Equal(5000m - (2 * 250m), await shop.OnHandAsync(water.Id));

        var consumption = (await shop.Client.GetFromJsonAsync<List<InventoryMovementDto>>("/inventory/movements", JsonOptions))!.Where(m => m.Type == MovementType.Consumption).ToList();
        Assert.Equal(2, consumption.Count);
        Assert.All(consumption, row => Assert.Equal(meal.Id, row.ItemId));
    }

    [Fact]
    public async Task A_modifier_whose_ingredient_cannot_cover_another_selection_reads_as_out_of_stock_and_cannot_be_ordered()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var shop = await OpenShopAsync(factory);
        var syrup = await shop.IngredientAsync("Coke Zero Syrup", 1000m);
        var water = await shop.IngredientAsync("Carbonated Water", 5000m);
        var (meal, _) = await shop.MealWithCokeZeroAsync((syrup, 30m), (water, 250m));
        Assert.False((await shop.CokeZeroAsync(meal.Id)).IsOutOfStock);

        _ = await shop.SetOnHandAsync(syrup.Id, 10m); // less than one selection needs

        var cokeZero = await shop.CokeZeroAsync(meal.Id);
        Assert.True(cokeZero.IsOutOfStock);
        // The kiosk reads the same flag from the full list, which must not be served stale.
        var all = (await shop.Client.GetFromJsonAsync<List<ModifierGroupDto>>("/modifier-groups", JsonOptions))!;
        Assert.True(all.Single().Modifiers.Single().IsOutOfStock);

        var refused = await shop.AddAsync(meal, cokeZero, 1m);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains("Coke Zero is sold out", await refused.Content.ReadAsStringAsync());

        // Restocking brings the option back.
        _ = await shop.SetOnHandAsync(syrup.Id, 500m);
        Assert.False((await shop.CokeZeroAsync(meal.Id)).IsOutOfStock);
        Assert.Equal(HttpStatusCode.OK, (await shop.AddAsync(meal, cokeZero, 1m)).StatusCode);
    }

    [Fact]
    public async Task An_ingredient_with_no_amount_only_checks_availability_and_is_never_deducted()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var shop = await OpenShopAsync(factory);
        var cups = await shop.IngredientAsync("Paper Cups", 50m);
        var (meal, cokeZero) = await shop.MealWithCokeZeroAsync((cups, null));

        Assert.Equal(HttpStatusCode.OK, (await shop.AddAsync(meal, cokeZero, 3m)).StatusCode);
        await shop.PayAsync();
        Assert.Equal(50m, await shop.OnHandAsync(cups.Id));

        _ = await shop.SetOnHandAsync(cups.Id, 0m);
        Assert.True((await shop.CokeZeroAsync(meal.Id)).IsOutOfStock);
    }

    [Fact]
    public async Task Without_separate_tracking_modifier_ingredients_neither_block_nor_deduct()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var shop = await OpenShopAsync(factory, separateTracking: false);
        var syrup = await shop.IngredientAsync("Coke Zero Syrup", 0m);
        var (meal, cokeZero) = await shop.MealWithCokeZeroAsync((syrup, 30m));

        Assert.False((await shop.CokeZeroAsync(meal.Id)).IsOutOfStock);
        Assert.Equal(HttpStatusCode.OK, (await shop.AddAsync(meal, cokeZero, 1m)).StatusCode);
        await shop.PayAsync();
        Assert.Equal(0m, await shop.OnHandAsync(syrup.Id));
    }

    [Fact]
    public async Task A_modifier_can_be_renamed_and_repriced_and_lists_its_ingredients_by_name()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var shop = await OpenShopAsync(factory);
        var syrup = await shop.IngredientAsync("Coke Zero Syrup", 1000m);
        var (meal, cokeZero) = await shop.MealWithCokeZeroAsync((syrup, 30m));

        var updated = await shop.Client.PutAsJsonAsync($"/modifiers/{cokeZero.Id}", new UpdateItemModifierRequest("Coke Zero 12oz", 30m));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        var listed = await shop.CokeZeroAsync(meal.Id);
        Assert.Equal("Coke Zero 12oz", listed.Name);
        Assert.Equal(30m, listed.PriceDelta);
        var ingredient = Assert.Single(listed.Ingredients!);
        Assert.Equal("Coke Zero Syrup", ingredient.InventoryItemName);
        Assert.Equal(30m, ingredient.QuantityPerOrder);
    }

    [Fact]
    public async Task Replacing_a_modifiers_ingredients_rejects_duplicates_unknown_items_and_zero_amounts()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var shop = await OpenShopAsync(factory);
        var syrup = await shop.IngredientAsync("Coke Zero Syrup", 1000m);
        var (_, cokeZero) = await shop.MealWithCokeZeroAsync((syrup, 30m));

        var duplicate = await shop.Client.PutAsJsonAsync(
            $"/modifiers/{cokeZero.Id}/ingredients",
            new ReplaceModifierIngredientsRequest([new ReplaceModifierIngredientLine(syrup.Id, 1m), new ReplaceModifierIngredientLine(syrup.Id, 2m)]));
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);

        var unknown = await shop.Client.PutAsJsonAsync(
            $"/modifiers/{cokeZero.Id}/ingredients",
            new ReplaceModifierIngredientsRequest([new ReplaceModifierIngredientLine(Guid.NewGuid(), 1m)]));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);

        var zero = await shop.Client.PutAsJsonAsync(
            $"/modifiers/{cokeZero.Id}/ingredients",
            new ReplaceModifierIngredientsRequest([new ReplaceModifierIngredientLine(syrup.Id, 0m)]));
        Assert.Equal(HttpStatusCode.BadRequest, zero.StatusCode);
    }
}
