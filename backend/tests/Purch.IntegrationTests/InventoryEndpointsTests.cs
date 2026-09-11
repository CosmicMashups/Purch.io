using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Purch.Application.Auth;
using Purch.Application.Catalog;
using Purch.Application.Inventory;
using Purch.Application.Onboarding;
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
