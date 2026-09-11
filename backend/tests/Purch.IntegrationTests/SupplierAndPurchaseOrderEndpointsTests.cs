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
public sealed class SupplierAndPurchaseOrderEndpointsTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Creating_a_supplier_makes_it_listable()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var response = await client.PostAsJsonAsync(
            "/suppliers",
            new CreateSupplierRequest("Acme Distribution", "09171234567"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<SupplierDto>(JsonOptions);
        Assert.Equal("Acme Distribution", created!.Name);
        Assert.True(created.IsActive);

        var list = await client.GetFromJsonAsync<List<SupplierDto>>("/suppliers", JsonOptions);
        Assert.Contains(list!, supplier => supplier.Name == "Acme Distribution");
    }

    [Fact]
    public async Task Creating_a_purchase_order_starts_it_as_draft()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var supplierId = await CreateSupplierAsync(client);
        var branchId = await MainBranchIdAsync(client);
        var item = await CreateItemAsync(client, "Bottled Water", 15m);

        var response = await client.PostAsJsonAsync(
            "/purchase-orders",
            new CreatePurchaseOrderRequest(
                supplierId,
                branchId,
                [new CreatePurchaseOrderLineRequest(item.Id, 100m, 10m)]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var purchaseOrder = await response.Content.ReadFromJsonAsync<PurchaseOrderDto>(JsonOptions);
        Assert.Equal(PurchaseOrderStatus.Draft, purchaseOrder!.Status);
        var line = Assert.Single(purchaseOrder.Lines);
        Assert.Equal(100m, line.QuantityOrdered);
        Assert.Equal(0m, line.QuantityReceived);
    }

    [Fact]
    public async Task Receiving_against_a_draft_purchase_order_is_rejected()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var supplierId = await CreateSupplierAsync(client);
        var branchId = await MainBranchIdAsync(client);
        var item = await CreateItemAsync(client, "Canned Goods", 30m);

        var createResponse = await client.PostAsJsonAsync(
            "/purchase-orders",
            new CreatePurchaseOrderRequest(
                supplierId,
                branchId,
                [new CreatePurchaseOrderLineRequest(item.Id, 50m, 20m)]));
        var purchaseOrder = await createResponse.Content.ReadFromJsonAsync<PurchaseOrderDto>(JsonOptions);
        var lineId = purchaseOrder!.Lines.Single().Id;

        var response = await client.PostAsJsonAsync(
            $"/purchase-orders/{purchaseOrder.Id}/receive",
            new ReceivePurchaseOrderRequest([new ReceivePurchaseOrderLineRequest(lineId, 50m)]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Partially_receiving_a_sent_order_marks_it_partially_received_and_adds_stock()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var supplierId = await CreateSupplierAsync(client);
        var branchId = await MainBranchIdAsync(client);
        var item = await CreateItemAsync(client, "Detergent", 45m);

        var createResponse = await client.PostAsJsonAsync(
            "/purchase-orders",
            new CreatePurchaseOrderRequest(
                supplierId,
                branchId,
                [new CreatePurchaseOrderLineRequest(item.Id, 100m, 25m)]));
        var purchaseOrder = await createResponse.Content.ReadFromJsonAsync<PurchaseOrderDto>(JsonOptions);
        var lineId = purchaseOrder!.Lines.Single().Id;

        _ = await client.PostAsync($"/purchase-orders/{purchaseOrder.Id}/mark-sent", null);

        var receiveResponse = await client.PostAsJsonAsync(
            $"/purchase-orders/{purchaseOrder.Id}/receive",
            new ReceivePurchaseOrderRequest([new ReceivePurchaseOrderLineRequest(lineId, 40m)]));

        Assert.Equal(HttpStatusCode.OK, receiveResponse.StatusCode);
        var afterFirstReceive = await receiveResponse.Content.ReadFromJsonAsync<PurchaseOrderDto>(JsonOptions);
        Assert.Equal(PurchaseOrderStatus.PartiallyReceived, afterFirstReceive!.Status);
        Assert.Equal(40m, afterFirstReceive.Lines.Single().QuantityReceived);

        var items = await client.GetFromJsonAsync<List<ItemDto>>("/items", JsonOptions);
        Assert.Equal(40m, items!.Single(i => i.Id == item.Id).StockOnHand);

        var finalReceiveResponse = await client.PostAsJsonAsync(
            $"/purchase-orders/{purchaseOrder.Id}/receive",
            new ReceivePurchaseOrderRequest([new ReceivePurchaseOrderLineRequest(lineId, 60m)]));
        var afterFinalReceive = await finalReceiveResponse.Content.ReadFromJsonAsync<PurchaseOrderDto>(JsonOptions);

        Assert.Equal(PurchaseOrderStatus.Received, afterFinalReceive!.Status);
        var finalItems = await client.GetFromJsonAsync<List<ItemDto>>("/items", JsonOptions);
        Assert.Equal(100m, finalItems!.Single(i => i.Id == item.Id).StockOnHand);
    }

    [Fact]
    public async Task Receiving_more_than_ordered_is_rejected()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var supplierId = await CreateSupplierAsync(client);
        var branchId = await MainBranchIdAsync(client);
        var item = await CreateItemAsync(client, "Pencil", 10m);

        var createResponse = await client.PostAsJsonAsync(
            "/purchase-orders",
            new CreatePurchaseOrderRequest(
                supplierId,
                branchId,
                [new CreatePurchaseOrderLineRequest(item.Id, 10m, 5m)]));
        var purchaseOrder = await createResponse.Content.ReadFromJsonAsync<PurchaseOrderDto>(JsonOptions);
        var lineId = purchaseOrder!.Lines.Single().Id;
        _ = await client.PostAsync($"/purchase-orders/{purchaseOrder.Id}/mark-sent", null);

        var response = await client.PostAsJsonAsync(
            $"/purchase-orders/{purchaseOrder.Id}/receive",
            new ReceivePurchaseOrderRequest([new ReceivePurchaseOrderLineRequest(lineId, 15m)]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Cancelling_a_sent_order_is_allowed_but_cancelling_a_received_one_is_not()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var supplierId = await CreateSupplierAsync(client);
        var branchId = await MainBranchIdAsync(client);
        var item = await CreateItemAsync(client, "Notebook", 12m);

        var createResponse = await client.PostAsJsonAsync(
            "/purchase-orders",
            new CreatePurchaseOrderRequest(
                supplierId,
                branchId,
                [new CreatePurchaseOrderLineRequest(item.Id, 10m, 5m)]));
        var purchaseOrder = await createResponse.Content.ReadFromJsonAsync<PurchaseOrderDto>(JsonOptions);
        _ = await client.PostAsync($"/purchase-orders/{purchaseOrder!.Id}/mark-sent", null);

        var cancelResponse = await client.PostAsync($"/purchase-orders/{purchaseOrder.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
        var cancelled = await cancelResponse.Content.ReadFromJsonAsync<PurchaseOrderDto>(JsonOptions);
        Assert.Equal(PurchaseOrderStatus.Cancelled, cancelled!.Status);

        var reCancelResponse = await client.PostAsync($"/purchase-orders/{purchaseOrder.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.BadRequest, reCancelResponse.StatusCode);
    }

    private static async Task<ItemDto> CreateItemAsync(HttpClient client, string name, decimal price)
    {
        var response = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest(name, null, null, null, price, null, PricingType.Unit));
        return (await response.Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;
    }

    private static async Task<Guid> CreateSupplierAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/suppliers",
            new CreateSupplierRequest($"Supplier-{Guid.NewGuid():N}", null));
        var supplier = await response.Content.ReadFromJsonAsync<SupplierDto>(JsonOptions);
        return supplier!.Id;
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
