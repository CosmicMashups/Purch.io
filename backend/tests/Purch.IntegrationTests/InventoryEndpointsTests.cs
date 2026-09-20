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

[Collection(PostgresCollectionDefinition.Name)]
public sealed class InventoryEndpointsTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Recording_a_stock_in_increases_the_items_stock_on_hand()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var branchId = await MainBranchIdAsync(client);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Instant Noodles", null, null, null, 12m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        var response = await client.PostAsJsonAsync(
            "/inventory/movements",
            new RecordMovementRequest(item!.Id, branchId, MovementType.StockIn, 50m, "Initial delivery", null, null, null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var movement = await response.Content.ReadFromJsonAsync<InventoryMovementDto>(JsonOptions);
        Assert.Equal(MovementType.StockIn, movement!.Type);
        Assert.Equal(50m, movement.Quantity);

        var items = await client.GetFromJsonAsync<List<ItemDto>>("/items", JsonOptions);
        Assert.Equal(50m, items!.Single(i => i.Id == item.Id).StockOnHand);
    }

    [Fact]
    public async Task Recording_a_stock_out_decreases_the_items_stock_on_hand()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var branchId = await MainBranchIdAsync(client);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Canned Goods", null, null, null, 30m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        _ = await client.PostAsJsonAsync(
            "/inventory/movements",
            new RecordMovementRequest(item!.Id, branchId, MovementType.StockIn, 20m, null, null, null, null));
        _ = await client.PostAsJsonAsync(
            "/inventory/movements",
            new RecordMovementRequest(item.Id, branchId, MovementType.StockOut, 5m, null, null, null, null));

        var items = await client.GetFromJsonAsync<List<ItemDto>>("/items", JsonOptions);
        Assert.Equal(15m, items!.Single(i => i.Id == item.Id).StockOnHand);
    }

    [Fact]
    public async Task A_negative_adjustment_reduces_stock_and_a_positive_one_increases_it()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var branchId = await MainBranchIdAsync(client);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Bar Soap", null, null, null, 15m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        _ = await client.PostAsJsonAsync(
            "/inventory/movements",
            new RecordMovementRequest(item!.Id, branchId, MovementType.Adjustment, -3m, "Miscount correction", null, null, null));

        var afterNegative = await client.GetFromJsonAsync<List<ItemDto>>("/items", JsonOptions);
        Assert.Equal(-3m, afterNegative!.Single(i => i.Id == item.Id).StockOnHand);

        _ = await client.PostAsJsonAsync(
            "/inventory/movements",
            new RecordMovementRequest(item.Id, branchId, MovementType.Adjustment, 10m, "Recount correction", null, null, null));

        var afterPositive = await client.GetFromJsonAsync<List<ItemDto>>("/items", JsonOptions);
        Assert.Equal(7m, afterPositive!.Single(i => i.Id == item.Id).StockOnHand);
    }

    [Fact]
    public async Task Recording_spoiled_stock_without_a_reason_category_is_rejected()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var branchId = await MainBranchIdAsync(client);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Milk", null, null, null, 60m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        var response = await client.PostAsJsonAsync(
            "/inventory/movements",
            new RecordMovementRequest(item!.Id, branchId, MovementType.Spoiled, 2m, null, null, null, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Recording_a_for_return_movement_without_a_supplier_reference_is_rejected()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var branchId = await MainBranchIdAsync(client);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Detergent", null, null, null, 45m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        var response = await client.PostAsJsonAsync(
            "/inventory/movements",
            new RecordMovementRequest(item!.Id, branchId, MovementType.ForReturn, 1m, null, null, null, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Listing_movements_can_be_filtered_by_item()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var branchId = await MainBranchIdAsync(client);

        var firstItemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Item A", null, null, null, 10m, null, PricingType.Unit));
        var firstItem = await firstItemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);
        var secondItemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Item B", null, null, null, 10m, null, PricingType.Unit));
        var secondItem = await secondItemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        _ = await client.PostAsJsonAsync(
            "/inventory/movements",
            new RecordMovementRequest(firstItem!.Id, branchId, MovementType.StockIn, 5m, null, null, null, null));
        _ = await client.PostAsJsonAsync(
            "/inventory/movements",
            new RecordMovementRequest(secondItem!.Id, branchId, MovementType.StockIn, 5m, null, null, null, null));

        var filtered = await client.GetFromJsonAsync<List<InventoryMovementDto>>(
            $"/inventory/movements?itemId={firstItem.Id}", JsonOptions);

        var movement = Assert.Single(filtered!);
        Assert.Equal(firstItem.Id, movement.ItemId);
    }

    [Fact]
    public async Task With_separate_inventory_tracking_stock_changes_and_sales_all_use_the_linked_inventory_item()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var branchId = await MainBranchIdAsync(client);

        var toggle = await client.PutAsJsonAsync("/tenant/settings/inventory-tracking", new UpdateInventoryTrackingSettingRequest(true));
        Assert.Equal(HttpStatusCode.OK, toggle.StatusCode);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Bottled Water", null, null, null, 15m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);
        _ = await client.PutAsJsonAsync($"/items/{item!.Id}/low-stock-threshold", new UpdateLowStockThresholdRequest(10m));

        // A delivery recorded against the item must land on the record the cashier reads.
        var movement = await client.PostAsJsonAsync(
            "/inventory/movements",
            new RecordMovementRequest(item.Id, branchId, MovementType.StockIn, 25m, null, null, null, null));
        Assert.Equal(HttpStatusCode.OK, movement.StatusCode);
        Assert.Equal(25m, await LinkedQuantityAsync(client, item.Id));
        Assert.Equal(0m, (await client.GetFromJsonAsync<List<ItemDto>>("/items", JsonOptions))!.Single(i => i.Id == item.Id).StockOnHand);

        // ...and a sale draws down that same record.
        _ = await client.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(item.Id, null, 2m));
        var payment = await client.PostAsJsonAsync("/transactions/cart/payments", new RecordPaymentRequest(PaymentMethod.Cash, 100m));
        Assert.Equal(HttpStatusCode.OK, payment.StatusCode);
        Assert.Equal(23m, await LinkedQuantityAsync(client, item.Id));

        // A correction moves it too, and the low-stock dashboard reads it (13 is above the threshold, 8 is not).
        _ = await client.PostAsJsonAsync(
            "/inventory/movements",
            new RecordMovementRequest(item.Id, branchId, MovementType.StockOut, 15m, null, null, null, null));
        Assert.Equal(8m, await LinkedQuantityAsync(client, item.Id));
        var dashboard = await client.GetFromJsonAsync<InventoryDashboardDto>("/inventory/dashboard", JsonOptions);
        var alert = Assert.Single(dashboard!.LowStockItems);
        Assert.Equal(8m, alert.StockOnHand);
    }

    private static async Task<(ItemDto Item, InventoryItemDto Ingredient)> RecipeSetupAsync(HttpClient client, string itemName)
    {
        var toggle = await client.PutAsJsonAsync("/tenant/settings/inventory-tracking", new UpdateInventoryTrackingSettingRequest(true));
        Assert.Equal(HttpStatusCode.OK, toggle.StatusCode);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest(itemName, null, null, null, 90m, null, PricingType.Unit));
        var item = (await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;

        var ingredientResponse = await client.PostAsJsonAsync(
            "/inventory-items",
            new CreateInventoryItemRequest("Coffee Beans", null, "g", "kg", 1000m, null));
        var ingredient = (await ingredientResponse.Content.ReadFromJsonAsync<InventoryItemDto>(JsonOptions))!;
        return (item, ingredient);
    }

    private static Task<HttpResponseMessage> SetRecipeAsync(HttpClient client, Guid itemId, params Guid[] ingredientIds)
    {
        return client.PutAsJsonAsync(
            $"/items/{itemId}/recipe",
            new ReplaceItemRecipeRequest([.. ingredientIds.Select(id => new ReplaceItemRecipeLineRequest(id, 18m))]));
    }

    [Fact]
    public async Task Giving_an_item_a_recipe_retires_its_own_inventory_item_and_clearing_it_restores_one()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var (item, ingredient) = await RecipeSetupAsync(client, "Latte");

        var before = await client.GetFromJsonAsync<List<InventoryItemDto>>("/inventory-items", JsonOptions);
        _ = Assert.Single(before!, i => i.LinkedItemId == item.Id && i.IsActive);

        var withRecipe = await SetRecipeAsync(client, item.Id, ingredient.Id);
        Assert.Equal(HttpStatusCode.OK, withRecipe.StatusCode);

        var during = await client.GetFromJsonAsync<List<InventoryItemDto>>("/inventory-items", JsonOptions);
        Assert.DoesNotContain(during!, i => i.LinkedItemId == item.Id);
        Assert.DoesNotContain(during!, i => i.IsAutoCreatedForItem && i.IsActive);

        var cleared = await SetRecipeAsync(client, item.Id);
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);

        var after = await client.GetFromJsonAsync<List<InventoryItemDto>>("/inventory-items", JsonOptions);
        _ = Assert.Single(after!, i => i.LinkedItemId == item.Id && i.IsActive);
    }

    [Fact]
    public async Task An_item_that_still_has_its_own_stock_cannot_be_given_a_recipe()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var branchId = await MainBranchIdAsync(client);
        var (item, ingredient) = await RecipeSetupAsync(client, "Iced Tea");

        _ = await client.PostAsJsonAsync(
            "/inventory/movements",
            new RecordMovementRequest(item.Id, branchId, MovementType.StockIn, 5m, null, null, null, null));

        var response = await SetRecipeAsync(client, item.Id, ingredient.Id);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var inventoryItems = await client.GetFromJsonAsync<List<InventoryItemDto>>("/inventory-items", JsonOptions);
        Assert.Equal(5m, inventoryItems!.Single(i => i.LinkedItemId == item.Id).QuantityOnHand);
    }

    [Fact]
    public async Task A_recipe_item_has_no_stock_of_its_own_so_manual_stock_changes_are_rejected()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var branchId = await MainBranchIdAsync(client);
        var (item, ingredient) = await RecipeSetupAsync(client, "Mocha");
        _ = await SetRecipeAsync(client, item.Id, ingredient.Id);

        var response = await client.PostAsJsonAsync(
            "/inventory/movements",
            new RecordMovementRequest(item.Id, branchId, MovementType.StockIn, 10m, null, null, null, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Ingredient_movements_are_named_in_the_log_instead_of_showing_as_a_deleted_item()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var branchId = await MainBranchIdAsync(client);
        var (item, ingredient) = await RecipeSetupAsync(client, "Flat White");
        _ = await SetRecipeAsync(client, item.Id, ingredient.Id);

        var receive = await client.PostAsJsonAsync(
            $"/inventory-items/{ingredient.Id}/receive",
            new ReceiveInventoryStockRequest(2m, branchId, null));
        Assert.Equal(HttpStatusCode.OK, receive.StatusCode);

        _ = await client.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(item.Id, null, 1m));
        var payment = await client.PostAsJsonAsync("/transactions/cart/payments", new RecordPaymentRequest(PaymentMethod.Cash, 100m));
        Assert.Equal(HttpStatusCode.OK, payment.StatusCode);

        var movements = await client.GetFromJsonAsync<List<InventoryMovementDto>>("/inventory/movements", JsonOptions);

        Assert.DoesNotContain(movements!, m => m.ItemName == "(deleted item)");
        Assert.Contains(movements!, m => m.Type == MovementType.StockIn && m.ItemName == "Coffee Beans");
        Assert.Contains(movements!, m => m.Type == MovementType.Consumption && m.ItemName == "Flat White");
    }

    [Fact]
    public async Task A_sale_movement_cannot_be_recorded_by_hand()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var branchId = await MainBranchIdAsync(client);
        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Candy", null, null, null, 5m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        var response = await client.PostAsJsonAsync(
            "/inventory/movements",
            new RecordMovementRequest(item!.Id, branchId, MovementType.Sale, 3m, null, null, null, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Services_and_combos_are_never_out_of_stock_and_stay_out_of_the_stock_alerts()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var haircut = (await (await client.PostAsJsonAsync("/items", new CreateItemRequest("Haircut", null, null, null, 200m, null, PricingType.Service))).Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;
        var meal = (await (await client.PostAsJsonAsync("/items", new CreateItemRequest("Value Meal", null, null, null, 150m, null, PricingType.Combo))).Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;
        var water = (await (await client.PostAsJsonAsync("/items", new CreateItemRequest("Water", null, null, null, 15m, null, PricingType.Unit))).Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;
        foreach (var item in new[] { haircut, meal, water })
        {
            _ = await client.PutAsJsonAsync($"/items/{item.Id}/low-stock-threshold", new UpdateLowStockThresholdRequest(5m));
        }

        var byId = (await client.GetFromJsonAsync<List<ItemDto>>("/items", JsonOptions))!.ToDictionary(i => i.Id);
        Assert.False(byId[haircut.Id].IsOutOfStock);
        Assert.False(byId[meal.Id].IsOutOfStock);
        Assert.True(byId[water.Id].IsOutOfStock);

        // Only the real, empty unit item is an alert; the service and the combo have nothing to restock.
        var dashboard = await client.GetFromJsonAsync<InventoryDashboardDto>("/inventory/dashboard", JsonOptions);
        Assert.Equal(1, dashboard!.OutOfStockCount);
        var csv = await (await client.GetAsync("/reports/inventory/low-stock-export.csv")).Content.ReadAsStringAsync();
        Assert.Contains("Water", csv);
        Assert.DoesNotContain("Haircut", csv);
        Assert.DoesNotContain("Value Meal", csv);
    }

    [Fact]
    public async Task With_separate_tracking_receiving_a_purchase_order_adds_to_the_linked_inventory_item()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var branchId = await MainBranchIdAsync(client);
        _ = await client.PutAsJsonAsync("/tenant/settings/inventory-tracking", new UpdateInventoryTrackingSettingRequest(true));
        var item = (await (await client.PostAsJsonAsync("/items", new CreateItemRequest("Flour", null, null, null, 40m, null, PricingType.Unit))).Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;
        var supplier = (await (await client.PostAsJsonAsync("/suppliers", new CreateSupplierRequest("Acme Distribution", null))).Content.ReadFromJsonAsync<SupplierDto>(JsonOptions))!;
        var order = (await (await client.PostAsJsonAsync(
            "/purchase-orders",
            new CreatePurchaseOrderRequest(supplier.Id, branchId, [new CreatePurchaseOrderLineRequest(item.Id, 30m, 10m)]))).Content.ReadFromJsonAsync<PurchaseOrderDto>(JsonOptions))!;
        _ = await client.PostAsync($"/purchase-orders/{order.Id}/mark-sent", null);

        var receive = await client.PostAsJsonAsync(
            $"/purchase-orders/{order.Id}/receive",
            new ReceivePurchaseOrderRequest([new ReceivePurchaseOrderLineRequest(order.Lines.Single().Id, 30m)]));

        Assert.Equal(HttpStatusCode.OK, receive.StatusCode);
        Assert.Equal(30m, await LinkedQuantityAsync(client, item.Id));
        Assert.Equal(0m, (await client.GetFromJsonAsync<List<ItemDto>>("/items", JsonOptions))!.Single(i => i.Id == item.Id).StockOnHand);
    }

    private static async Task<decimal> LinkedQuantityAsync(HttpClient client, Guid itemId)
    {
        var inventoryItems = await client.GetFromJsonAsync<List<InventoryItemDto>>("/inventory-items", JsonOptions);
        return inventoryItems!.Single(i => i.LinkedItemId == itemId).QuantityOnHand;
    }

    private static async Task<Guid> MainBranchIdAsync(HttpClient client)
    {
        var branches = await client.GetFromJsonAsync<List<BranchDto>>("/branches", JsonOptions);
        return branches!.Single().Id;
    }

    private static async Task<HttpClient> AuthenticatedAdminClientAsync(PurchApiFactory factory)
    {
        var client = factory.CreateClient();

        var bootstrapResponse = await client.PostAsJsonAsync(
            "/onboarding/bootstrap",
            new BootstrapTenantRequest(
                $"Tenant-{Guid.NewGuid():N}",
                BusinessType.ConvenienceStore,
                "Main Branch",
                "Admin User",
                "1234"));
        var bootstrapResult = await bootstrapResponse.Content.ReadFromJsonAsync<BootstrapTenantResult>(JsonOptions);

        var loginResponse = await client.PostAsJsonAsync(
            "/auth/login",
            new LoginRequest(bootstrapResult!.DevicePairingCode, "1234"));
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<LoginResponseBody>(JsonOptions);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginBody!.AccessToken);
        return client;
    }

    private sealed record LoginResponseBody(string AccessToken);
}
