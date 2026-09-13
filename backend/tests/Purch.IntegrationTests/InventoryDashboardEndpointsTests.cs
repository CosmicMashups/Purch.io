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
public sealed class InventoryDashboardEndpointsTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Setting_a_low_stock_threshold_is_reflected_on_the_item()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Rice", null, null, null, 55m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        var response = await client.PutAsJsonAsync(
            $"/items/{item!.Id}/low-stock-threshold",
            new UpdateLowStockThresholdRequest(10m));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);
        Assert.Equal(10m, updated!.LowStockThreshold);
    }

    [Fact]
    public async Task The_dashboard_counts_out_of_stock_and_low_stock_items_separately()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var outOfStockItemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Salt", null, null, null, 10m, null, PricingType.Unit));
        var outOfStockItem = await outOfStockItemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        var lowStockItemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Sugar", null, null, null, 20m, null, PricingType.Unit));
        var lowStockItem = await lowStockItemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);
        _ = await client.PutAsJsonAsync(
            $"/items/{lowStockItem!.Id}/low-stock-threshold",
            new UpdateLowStockThresholdRequest(5m));
        _ = await client.PostAsJsonAsync(
            "/inventory/movements",
            new RecordMovementRequest(lowStockItem.Id, await MainBranchIdAsync(client), MovementType.StockIn, 3m, null, null, null, null));

        var healthyItemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Cooking Oil", null, null, null, 80m, null, PricingType.Unit));
        var healthyItem = await healthyItemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);
        _ = await client.PutAsJsonAsync(
            $"/items/{healthyItem!.Id}/low-stock-threshold",
            new UpdateLowStockThresholdRequest(5m));
        _ = await client.PostAsJsonAsync(
            "/inventory/movements",
            new RecordMovementRequest(healthyItem.Id, await MainBranchIdAsync(client), MovementType.StockIn, 50m, null, null, null, null));

        var response = await client.GetAsync("/inventory/dashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dashboard = await response.Content.ReadFromJsonAsync<InventoryDashboardDto>(JsonOptions);
        Assert.Equal(3, dashboard!.TotalSkus);
        Assert.Equal(1, dashboard.OutOfStockCount);
        Assert.Equal(1, dashboard.LowStockCount);
        var lowStockAlert = Assert.Single(dashboard.LowStockItems);
        Assert.Equal(lowStockItem.Id, lowStockAlert.ItemId);
        Assert.DoesNotContain(dashboard.LowStockItems, alert => alert.ItemId == outOfStockItem!.Id);
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
