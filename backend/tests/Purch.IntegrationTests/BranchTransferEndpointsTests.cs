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
public sealed class BranchTransferEndpointsTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Creating_a_transfer_starts_it_as_pending_without_touching_stock()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var mainBranchId = await MainBranchIdAsync(client);
        var secondBranchId = await CreateBranchAsync(client, "Branch 2");

        var item = await CreateItemAsync(client, "Bottled Water", 15m);
        _ = await client.PostAsJsonAsync(
            "/inventory/movements",
            new RecordMovementRequest(item.Id, mainBranchId, MovementType.StockIn, 100m, null, null, null, null));

        var response = await client.PostAsJsonAsync(
            "/branch-transfers",
            new CreateBranchTransferRequest(
                mainBranchId,
                secondBranchId,
                [new CreateBranchTransferLineRequest(item.Id, 20m)]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var transfer = await response.Content.ReadFromJsonAsync<BranchTransferDto>(JsonOptions);
        Assert.Equal(BranchTransferStatus.Pending, transfer!.Status);
        var line = Assert.Single(transfer.Lines);
        Assert.Equal(20m, line.Quantity);

        var items = await client.GetFromJsonAsync<List<ItemDto>>("/items", JsonOptions);
        Assert.Equal(100m, items!.Single(i => i.Id == item.Id).StockOnHand);
    }

    [Fact]
    public async Task Marking_in_transit_deducts_stock_and_marking_received_restores_the_net_total()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var mainBranchId = await MainBranchIdAsync(client);
        var secondBranchId = await CreateBranchAsync(client, "Branch 2");

        var item = await CreateItemAsync(client, "Canned Goods", 30m);
        _ = await client.PostAsJsonAsync(
            "/inventory/movements",
            new RecordMovementRequest(item.Id, mainBranchId, MovementType.StockIn, 100m, null, null, null, null));

        var createResponse = await client.PostAsJsonAsync(
            "/branch-transfers",
            new CreateBranchTransferRequest(
                mainBranchId,
                secondBranchId,
                [new CreateBranchTransferLineRequest(item.Id, 20m)]));
        var transfer = await createResponse.Content.ReadFromJsonAsync<BranchTransferDto>(JsonOptions);

        var inTransitResponse = await client.PostAsync($"/branch-transfers/{transfer!.Id}/mark-in-transit", null);
        Assert.Equal(HttpStatusCode.OK, inTransitResponse.StatusCode);
        var inTransit = await inTransitResponse.Content.ReadFromJsonAsync<BranchTransferDto>(JsonOptions);
        Assert.Equal(BranchTransferStatus.InTransit, inTransit!.Status);

        var itemsAfterShip = await client.GetFromJsonAsync<List<ItemDto>>("/items", JsonOptions);
        Assert.Equal(80m, itemsAfterShip!.Single(i => i.Id == item.Id).StockOnHand);

        var receivedResponse = await client.PostAsync($"/branch-transfers/{transfer.Id}/mark-received", null);
        Assert.Equal(HttpStatusCode.OK, receivedResponse.StatusCode);
        var received = await receivedResponse.Content.ReadFromJsonAsync<BranchTransferDto>(JsonOptions);
        Assert.Equal(BranchTransferStatus.Received, received!.Status);

        var itemsAfterReceive = await client.GetFromJsonAsync<List<ItemDto>>("/items", JsonOptions);
        Assert.Equal(100m, itemsAfterReceive!.Single(i => i.Id == item.Id).StockOnHand);
    }

    private static async Task<(BranchTransferDto Transfer, ItemDto Item, Guid MainBranchId)> StockedTransferAsync(HttpClient client, decimal stock, decimal quantity)
    {
        var mainBranchId = await MainBranchIdAsync(client);
        var secondBranchId = await CreateBranchAsync(client, "Branch 2");
        var item = await CreateItemAsync(client, "Canned Goods", 30m);
        if (stock > 0)
        {
            _ = await client.PostAsJsonAsync(
                "/inventory/movements",
                new RecordMovementRequest(item.Id, mainBranchId, MovementType.StockIn, stock, null, null, null, null));
        }

        var createResponse = await client.PostAsJsonAsync(
            "/branch-transfers",
            new CreateBranchTransferRequest(mainBranchId, secondBranchId, [new CreateBranchTransferLineRequest(item.Id, quantity)]));
        return ((await createResponse.Content.ReadFromJsonAsync<BranchTransferDto>(JsonOptions))!, item, mainBranchId);
    }

    private static async Task<decimal> StockOfAsync(HttpClient client, Guid itemId)
    {
        var items = await client.GetFromJsonAsync<List<ItemDto>>("/items", JsonOptions);
        return items!.Single(i => i.Id == itemId).StockOnHand;
    }

    [Fact]
    public async Task Shipping_more_than_is_on_hand_is_refused()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var (transfer, item, _) = await StockedTransferAsync(client, stock: 5m, quantity: 20m);

        var response = await client.PostAsync($"/branch-transfers/{transfer.Id}/mark-in-transit", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(5m, await StockOfAsync(client, item.Id));
    }

    [Fact]
    public async Task Cancelling_a_pending_transfer_leaves_stock_untouched()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var (transfer, item, _) = await StockedTransferAsync(client, stock: 100m, quantity: 20m);

        var response = await client.PostAsync($"/branch-transfers/{transfer.Id}/cancel", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(BranchTransferStatus.Cancelled, (await response.Content.ReadFromJsonAsync<BranchTransferDto>(JsonOptions))!.Status);
        Assert.Equal(100m, await StockOfAsync(client, item.Id));
    }

    [Fact]
    public async Task Cancelling_an_in_transit_transfer_puts_the_stock_back()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var (transfer, item, _) = await StockedTransferAsync(client, stock: 100m, quantity: 20m);
        _ = await client.PostAsync($"/branch-transfers/{transfer.Id}/mark-in-transit", null);
        Assert.Equal(80m, await StockOfAsync(client, item.Id));

        var response = await client.PostAsync($"/branch-transfers/{transfer.Id}/cancel", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(100m, await StockOfAsync(client, item.Id));

        // ...and it can't then be received, which would add the stock a second time.
        var receive = await client.PostAsync($"/branch-transfers/{transfer.Id}/mark-received", null);
        Assert.Equal(HttpStatusCode.BadRequest, receive.StatusCode);
    }

    [Fact]
    public async Task A_received_transfer_cannot_be_cancelled()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var (transfer, _, _) = await StockedTransferAsync(client, stock: 100m, quantity: 20m);
        _ = await client.PostAsync($"/branch-transfers/{transfer.Id}/mark-in-transit", null);
        _ = await client.PostAsync($"/branch-transfers/{transfer.Id}/mark-received", null);

        var response = await client.PostAsync($"/branch-transfers/{transfer.Id}/cancel", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Marking_a_pending_transfer_received_directly_is_rejected()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var mainBranchId = await MainBranchIdAsync(client);
        var secondBranchId = await CreateBranchAsync(client, "Branch 2");
        var item = await CreateItemAsync(client, "Detergent", 45m);

        var createResponse = await client.PostAsJsonAsync(
            "/branch-transfers",
            new CreateBranchTransferRequest(
                mainBranchId,
                secondBranchId,
                [new CreateBranchTransferLineRequest(item.Id, 5m)]));
        var transfer = await createResponse.Content.ReadFromJsonAsync<BranchTransferDto>(JsonOptions);

        var response = await client.PostAsync($"/branch-transfers/{transfer!.Id}/mark-received", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Creating_a_transfer_with_the_same_source_and_destination_is_rejected()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var mainBranchId = await MainBranchIdAsync(client);
        var item = await CreateItemAsync(client, "Pencil", 10m);

        var response = await client.PostAsJsonAsync(
            "/branch-transfers",
            new CreateBranchTransferRequest(
                mainBranchId,
                mainBranchId,
                [new CreateBranchTransferLineRequest(item.Id, 5m)]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<ItemDto> CreateItemAsync(HttpClient client, string name, decimal price)
    {
        var response = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest(name, null, null, null, price, null, PricingType.Unit));
        return (await response.Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;
    }

    private static async Task<Guid> CreateBranchAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/branches", new CreateBranchRequest(name, null));
        var branch = await response.Content.ReadFromJsonAsync<BranchDto>(JsonOptions);
        return branch!.Id;
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
