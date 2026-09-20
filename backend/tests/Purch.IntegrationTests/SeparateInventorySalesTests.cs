using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Purch.Application.Auth;
using Purch.Application.Catalog;
using Purch.Application.Inventory;
using Purch.Application.Onboarding;
using Purch.Application.Pos;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests;

/// <summary>End to end for a tenant that tracks stock separately from the Cashier catalog: a Cashier item is
/// either an inventory item (its own paired stock record) or a recipe built from other inventory items, and
/// checking one out moves the right stock and writes the right movement log entries.</summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class SeparateInventorySalesTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>A signed-in admin terminal for a fresh tenant, with helpers for the setup every scenario repeats.</summary>
    private sealed class Shop(HttpClient client, Guid branchId) : IDisposable
    {
        public HttpClient Client { get; } = client;

        public Guid BranchId { get; } = branchId;

        public void Dispose()
        {
            Client.Dispose();
        }

        /// <summary>An ingredient / raw material with a known starting quantity (1 packaging unit = 1 base unit, so
        /// quantities read plainly).</summary>
        public async Task<InventoryItemDto> IngredientAsync(string name, decimal onHand)
        {
            var created = (await (await Client.PostAsJsonAsync(
                "/inventory-items",
                new CreateInventoryItemRequest(name, null, "g", "g", 1m, null))).Content.ReadFromJsonAsync<InventoryItemDto>(JsonOptions))!;
            return await SetOnHandAsync(created.Id, onHand);
        }

        public async Task<InventoryItemDto> SetOnHandAsync(Guid inventoryItemId, decimal quantity)
        {
            var response = await Client.PostAsJsonAsync($"/inventory-items/{inventoryItemId}/physical-count", new UpdatePhysicalCountRequest(quantity, BranchId));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<InventoryItemDto>(JsonOptions))!;
        }

        /// <summary>A Cashier item that is itself an inventory item: its paired stock record holds the count.</summary>
        public async Task<ItemDto> InventoryBackedItemAsync(string name, decimal price, decimal onHand)
        {
            var item = (await (await Client.PostAsJsonAsync("/items", new CreateItemRequest(name, null, null, null, price, null, PricingType.Unit))).Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;
            _ = await SetOnHandAsync((await PairedRecordAsync(item.Id)).Id, onHand);
            return item;
        }

        /// <summary>A Cashier item made from ingredients: (ingredient, quantity used per order) pairs.</summary>
        public async Task<ItemDto> RecipeItemAsync(string name, decimal price, params (InventoryItemDto Ingredient, decimal? PerOrder)[] recipe)
        {
            var item = (await (await Client.PostAsJsonAsync("/items", new CreateItemRequest(name, null, null, null, price, null, PricingType.Unit))).Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;
            var response = await Client.PutAsJsonAsync(
                $"/items/{item.Id}/recipe",
                new ReplaceItemRecipeRequest([.. recipe.Select(line => new ReplaceItemRecipeLineRequest(line.Ingredient.Id, line.PerOrder))]));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return item;
        }

        public async Task<InventoryItemDto> PairedRecordAsync(Guid itemId)
        {
            return (await Client.GetFromJsonAsync<List<InventoryItemDto>>("/inventory-items", JsonOptions))!.Single(i => i.LinkedItemId == itemId);
        }

        public async Task<decimal> OnHandAsync(Guid inventoryItemId)
        {
            return (await Client.GetFromJsonAsync<List<InventoryItemDto>>("/inventory-items", JsonOptions))!.Single(i => i.Id == inventoryItemId).QuantityOnHand;
        }

        public async Task<ItemDto> ItemAsync(Guid itemId)
        {
            return (await Client.GetFromJsonAsync<List<ItemDto>>("/items", JsonOptions))!.Single(i => i.Id == itemId);
        }

        public async Task<List<InventoryMovementDto>> MovementsAsync(MovementType type)
        {
            return [.. (await Client.GetFromJsonAsync<List<InventoryMovementDto>>("/inventory/movements", JsonOptions))!.Where(m => m.Type == type)];
        }

        /// <summary>Rings up the lines and pays cash. Returns the completed sale.</summary>
        public async Task<TransactionDto> CheckOutAsync(params (ItemDto Item, decimal Quantity)[] lines)
        {
            foreach (var (item, quantity) in lines)
            {
                var add = await Client.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(item.Id, null, quantity));
                Assert.Equal(HttpStatusCode.OK, add.StatusCode);
            }

            var pay = await Client.PostAsJsonAsync("/transactions/cart/payments", new RecordPaymentRequest(PaymentMethod.Cash, 100000m));
            Assert.Equal(HttpStatusCode.OK, pay.StatusCode);
            var sale = (await pay.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions))!;
            Assert.Equal(TransactionStatus.Completed, sale.Status);
            return sale;
        }
    }

    private static async Task<Shop> OpenShopAsync(PurchApiFactory factory, bool separateTracking = true)
    {
        var client = factory.CreateClient();
        var bootstrap = await client.PostAsJsonAsync(
            "/onboarding/bootstrap",
            new BootstrapTenantRequest($"Tenant-{Guid.NewGuid():N}", BusinessType.ConvenienceStore, "Main Branch", "Admin User", "1234"));
        var tenant = (await bootstrap.Content.ReadFromJsonAsync<BootstrapTenantResult>(JsonOptions))!;
        var login = await client.PostAsJsonAsync("/auth/login", new LoginRequest(tenant.DevicePairingCode, "1234"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            (await login.Content.ReadFromJsonAsync<TokenBody>(JsonOptions))!.AccessToken);

        if (separateTracking)
        {
            var toggle = await client.PutAsJsonAsync("/tenant/settings/inventory-tracking", new UpdateInventoryTrackingSettingRequest(true));
            Assert.Equal(HttpStatusCode.OK, toggle.StatusCode);
        }

        var branchId = (await client.GetFromJsonAsync<List<BranchDto>>("/branches", JsonOptions))!.Single().Id;
        return new Shop(client, branchId);
    }

    [Fact]
    public async Task Checking_out_a_recipe_item_uses_up_each_ingredient_by_its_per_order_amount_times_the_quantity_sold()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var shop = await OpenShopAsync(factory);
        var beans = await shop.IngredientAsync("Coffee Beans", 1000m);
        var milk = await shop.IngredientAsync("Milk", 5000m);
        var latte = await shop.RecipeItemAsync("Latte", 120m, (beans, 18m), (milk, 200m));

        _ = await shop.CheckOutAsync((latte, 3m));

        Assert.Equal(1000m - (3 * 18m), await shop.OnHandAsync(beans.Id));
        Assert.Equal(5000m - (3 * 200m), await shop.OnHandAsync(milk.Id));
        // The recipe item holds no stock of its own, and no counter of its own moved.
        Assert.Equal(0m, (await shop.ItemAsync(latte.Id)).StockOnHand);
    }

    [Fact]
    public async Task A_recipe_sale_writes_one_consumption_movement_per_ingredient_and_no_sale_movement()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var shop = await OpenShopAsync(factory);
        var beans = await shop.IngredientAsync("Coffee Beans", 1000m);
        var milk = await shop.IngredientAsync("Milk", 5000m);
        var latte = await shop.RecipeItemAsync("Latte", 120m, (beans, 18m), (milk, 200m));

        _ = await shop.CheckOutAsync((latte, 2m));

        var consumption = await shop.MovementsAsync(MovementType.Consumption);
        Assert.Equal(2, consumption.Count);
        var beanRow = Assert.Single(consumption, m => m.Quantity == 36m);
        var milkRow = Assert.Single(consumption, m => m.Quantity == 400m);
        // Each row is attributed to the item that was sold, at the branch it was sold in, and names it.
        Assert.All(consumption, row =>
        {
            Assert.Equal(latte.Id, row.ItemId);
            Assert.Equal("Latte", row.ItemName);
            Assert.Equal(shop.BranchId, row.BranchId);
        });
        Assert.NotEqual(beanRow.Id, milkRow.Id);
        Assert.Empty(await shop.MovementsAsync(MovementType.Sale));
    }

    [Fact]
    public async Task Two_recipe_items_sharing_an_ingredient_use_it_up_together_in_one_sale()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var shop = await OpenShopAsync(factory);
        var beans = await shop.IngredientAsync("Coffee Beans", 1000m);
        var milk = await shop.IngredientAsync("Milk", 5000m);
        var chocolate = await shop.IngredientAsync("Chocolate", 800m);
        var latte = await shop.RecipeItemAsync("Latte", 120m, (beans, 18m), (milk, 200m));
        var mocha = await shop.RecipeItemAsync("Mocha", 140m, (beans, 18m), (milk, 150m), (chocolate, 30m));

        _ = await shop.CheckOutAsync((latte, 2m), (mocha, 1m));

        Assert.Equal(1000m - (2 * 18m) - 18m, await shop.OnHandAsync(beans.Id));
        Assert.Equal(5000m - (2 * 200m) - 150m, await shop.OnHandAsync(milk.Id));
        Assert.Equal(800m - 30m, await shop.OnHandAsync(chocolate.Id));
    }

    [Fact]
    public async Task An_ingredient_with_no_per_order_amount_is_not_consumed()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var shop = await OpenShopAsync(factory);
        var beans = await shop.IngredientAsync("Coffee Beans", 1000m);
        var napkins = await shop.IngredientAsync("Napkins", 500m);
        var drink = await shop.RecipeItemAsync("Drip Coffee", 80m, (beans, 15m), (napkins, null));

        _ = await shop.CheckOutAsync((drink, 4m));

        Assert.Equal(940m, await shop.OnHandAsync(beans.Id));
        Assert.Equal(500m, await shop.OnHandAsync(napkins.Id));
        _ = Assert.Single(await shop.MovementsAsync(MovementType.Consumption));
    }

    [Fact]
    public async Task Checking_out_an_inventory_item_takes_it_off_its_own_stock_record_and_logs_a_sale_movement()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var shop = await OpenShopAsync(factory);
        var water = await shop.InventoryBackedItemAsync("Bottled Water", 15m, 24m);

        _ = await shop.CheckOutAsync((water, 5m));

        var record = await shop.PairedRecordAsync(water.Id);
        Assert.Equal(19m, record.QuantityOnHand);
        Assert.Equal(0m, (await shop.ItemAsync(water.Id)).StockOnHand);

        var sale = Assert.Single(await shop.MovementsAsync(MovementType.Sale));
        Assert.Equal(water.Id, sale.ItemId);
        Assert.Equal(5m, sale.Quantity);
        Assert.Equal(shop.BranchId, sale.BranchId);
        Assert.Empty(await shop.MovementsAsync(MovementType.Consumption));
    }

    [Fact]
    public async Task A_cart_mixing_a_recipe_item_and_an_inventory_item_moves_each_kind_of_stock_correctly()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var shop = await OpenShopAsync(factory);
        var beans = await shop.IngredientAsync("Coffee Beans", 1000m);
        var latte = await shop.RecipeItemAsync("Latte", 120m, (beans, 18m));
        var cookie = await shop.InventoryBackedItemAsync("Cookie", 35m, 12m);

        _ = await shop.CheckOutAsync((latte, 2m), (cookie, 3m));

        Assert.Equal(964m, await shop.OnHandAsync(beans.Id));
        Assert.Equal(9m, (await shop.PairedRecordAsync(cookie.Id)).QuantityOnHand);
        _ = Assert.Single(await shop.MovementsAsync(MovementType.Consumption));
        _ = Assert.Single(await shop.MovementsAsync(MovementType.Sale));
    }

    [Fact]
    public async Task Successive_sales_keep_drawing_the_same_stock_down()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var shop = await OpenShopAsync(factory);
        var beans = await shop.IngredientAsync("Coffee Beans", 100m);
        var latte = await shop.RecipeItemAsync("Latte", 120m, (beans, 18m));

        _ = await shop.CheckOutAsync((latte, 1m));
        _ = await shop.CheckOutAsync((latte, 2m));

        Assert.Equal(100m - 18m - 36m, await shop.OnHandAsync(beans.Id));
        Assert.Equal(2, (await shop.MovementsAsync(MovementType.Consumption)).Count);
    }

    [Fact]
    public async Task A_recipe_item_reads_as_out_of_stock_once_an_ingredient_cannot_cover_another_order()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var shop = await OpenShopAsync(factory);
        var beans = await shop.IngredientAsync("Coffee Beans", 40m);
        var latte = await shop.RecipeItemAsync("Latte", 120m, (beans, 18m));
        Assert.False((await shop.ItemAsync(latte.Id)).IsOutOfStock);

        _ = await shop.CheckOutAsync((latte, 1m)); // 22 left: still enough for one more (18)
        Assert.False((await shop.ItemAsync(latte.Id)).IsOutOfStock);

        _ = await shop.CheckOutAsync((latte, 1m)); // 4 left: not enough for an order
        Assert.True((await shop.ItemAsync(latte.Id)).IsOutOfStock);

        // Restocking the ingredient brings it back.
        _ = await shop.SetOnHandAsync(beans.Id, 500m);
        Assert.False((await shop.ItemAsync(latte.Id)).IsOutOfStock);
    }

    [Fact]
    public async Task An_inventory_item_reads_as_out_of_stock_when_its_last_unit_is_sold()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var shop = await OpenShopAsync(factory);
        var cookie = await shop.InventoryBackedItemAsync("Cookie", 35m, 2m);
        Assert.False((await shop.ItemAsync(cookie.Id)).IsOutOfStock);

        _ = await shop.CheckOutAsync((cookie, 2m));

        Assert.True((await shop.ItemAsync(cookie.Id)).IsOutOfStock);
    }

    [Fact]
    public async Task A_cart_that_is_voided_instead_of_paid_consumes_nothing()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var shop = await OpenShopAsync(factory);
        var beans = await shop.IngredientAsync("Coffee Beans", 1000m);
        var latte = await shop.RecipeItemAsync("Latte", 120m, (beans, 18m));
        var cookie = await shop.InventoryBackedItemAsync("Cookie", 35m, 12m);

        _ = await shop.Client.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(latte.Id, null, 2m));
        _ = await shop.Client.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(cookie.Id, null, 3m));
        _ = await shop.Client.PostAsync("/transactions/cart/void", null);

        Assert.Equal(1000m, await shop.OnHandAsync(beans.Id));
        Assert.Equal(12m, (await shop.PairedRecordAsync(cookie.Id)).QuantityOnHand);
        Assert.Empty(await shop.MovementsAsync(MovementType.Consumption));
        Assert.Empty(await shop.MovementsAsync(MovementType.Sale));
    }

    [Fact]
    public async Task Turning_an_inventory_item_into_a_recipe_item_makes_a_sale_use_the_ingredients_instead_of_its_own_record()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var shop = await OpenShopAsync(factory);
        // Starts life as a plain Cashier item with an (empty) paired stock record...
        var beans = await shop.IngredientAsync("Coffee Beans", 1000m);
        var drink = (await (await shop.Client.PostAsJsonAsync("/items", new CreateItemRequest("House Blend", null, null, null, 90m, null, PricingType.Unit))).Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;
        var paired = await shop.PairedRecordAsync(drink.Id);

        // ...then gets a recipe: it may not be both, so its own record is retired.
        var recipe = await shop.Client.PutAsJsonAsync($"/items/{drink.Id}/recipe", new ReplaceItemRecipeRequest([new ReplaceItemRecipeLineRequest(beans.Id, 20m)]));
        Assert.Equal(HttpStatusCode.OK, recipe.StatusCode);

        _ = await shop.CheckOutAsync((drink, 2m));

        Assert.Equal(960m, await shop.OnHandAsync(beans.Id));
        Assert.Equal(0m, await shop.OnHandAsync(paired.Id));
        Assert.Empty(await shop.MovementsAsync(MovementType.Sale));
        _ = Assert.Single(await shop.MovementsAsync(MovementType.Consumption));
    }

    [Fact]
    public async Task Without_separate_tracking_a_recipe_is_ignored_and_the_items_own_stock_is_used()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var shop = await OpenShopAsync(factory, separateTracking: false);
        var beans = await shop.IngredientAsync("Coffee Beans", 1000m);
        var latte = await shop.RecipeItemAsync("Latte", 120m, (beans, 18m));
        _ = await shop.Client.PostAsJsonAsync(
            "/inventory/movements",
            new RecordMovementRequest(latte.Id, shop.BranchId, MovementType.StockIn, 10m, null, null, null, null));

        _ = await shop.CheckOutAsync((latte, 2m));

        Assert.Equal(8m, (await shop.ItemAsync(latte.Id)).StockOnHand);
        Assert.Equal(1000m, await shop.OnHandAsync(beans.Id));
        Assert.Empty(await shop.MovementsAsync(MovementType.Consumption));
        _ = Assert.Single(await shop.MovementsAsync(MovementType.Sale));
    }

    private sealed record TokenBody(string AccessToken);
}
