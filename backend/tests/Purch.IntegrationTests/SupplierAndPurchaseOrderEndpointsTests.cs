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

        var response = await ReceiveAsync(client, purchaseOrder!.Id, supplierId, branchId, item.Id, 50m);

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
        _ = await client.PostAsync($"/purchase-orders/{purchaseOrder!.Id}/mark-sent", null);

        var receiveResponse = await ReceiveAsync(client, purchaseOrder.Id, supplierId, branchId, item.Id, 40m);

        Assert.Equal(HttpStatusCode.OK, receiveResponse.StatusCode);
        var orders = await client.GetFromJsonAsync<List<PurchaseOrderDto>>("/purchase-orders", JsonOptions);
        var afterFirstReceive = orders!.Single(o => o.Id == purchaseOrder.Id);
        Assert.Equal(PurchaseOrderStatus.PartiallyReceived, afterFirstReceive.Status);
        Assert.Equal(40m, afterFirstReceive.Lines.Single().QuantityReceived);
        _ = Assert.Single(afterFirstReceive.Receipts);

        var items = await client.GetFromJsonAsync<List<ItemDto>>("/items", JsonOptions);
        Assert.Equal(40m, items!.Single(i => i.Id == item.Id).StockOnHand);

        _ = await ReceiveAsync(client, purchaseOrder.Id, supplierId, branchId, item.Id, 60m);
        var finalOrders = await client.GetFromJsonAsync<List<PurchaseOrderDto>>("/purchase-orders", JsonOptions);

        Assert.Equal(PurchaseOrderStatus.Received, finalOrders!.Single(o => o.Id == purchaseOrder.Id).Status);
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
        _ = await client.PostAsync($"/purchase-orders/{purchaseOrder!.Id}/mark-sent", null);

        var response = await ReceiveAsync(client, purchaseOrder.Id, supplierId, branchId, item.Id, 15m);

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

    [Fact]
    public async Task A_delivery_recorded_without_a_purchase_order_can_be_linked_later_without_adding_stock_twice()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var supplierId = await CreateSupplierAsync(client);
        var branchId = await MainBranchIdAsync(client);
        var item = await CreateItemAsync(client, "Rice", 60m);

        var irrResponse = await ReceiveAsync(client, null, supplierId, branchId, item.Id, 10m);
        Assert.Equal(HttpStatusCode.OK, irrResponse.StatusCode);
        var report = await irrResponse.Content.ReadFromJsonAsync<IncomingReceivingDto>(JsonOptions);
        Assert.Null(report!.PurchaseOrderId);

        var createResponse = await client.PostAsJsonAsync(
            "/purchase-orders",
            new CreatePurchaseOrderRequest(supplierId, branchId, [new CreatePurchaseOrderLineRequest(item.Id, 10m, 5m)]));
        var purchaseOrder = await createResponse.Content.ReadFromJsonAsync<PurchaseOrderDto>(JsonOptions);
        _ = await client.PostAsync($"/purchase-orders/{purchaseOrder!.Id}/mark-sent", null);

        var linkResponse = await client.PostAsJsonAsync(
            $"/incoming-receiving/{report.Id}/link-purchase-order",
            new LinkIncomingReceivingRequest(purchaseOrder.Id));
        Assert.Equal(HttpStatusCode.OK, linkResponse.StatusCode);

        var orders = await client.GetFromJsonAsync<List<PurchaseOrderDto>>("/purchase-orders", JsonOptions);
        Assert.Equal(PurchaseOrderStatus.Received, orders!.Single(o => o.Id == purchaseOrder.Id).Status);
        var items = await client.GetFromJsonAsync<List<ItemDto>>("/items", JsonOptions);
        Assert.Equal(10m, items!.Single(i => i.Id == item.Id).StockOnHand);
    }

    [Fact]
    public async Task Rejected_lines_add_no_stock_and_do_not_count_toward_the_order()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var supplierId = await CreateSupplierAsync(client);
        var branchId = await MainBranchIdAsync(client);
        var item = await CreateItemAsync(client, "Eggs", 8m);

        var createResponse = await client.PostAsJsonAsync(
            "/purchase-orders",
            new CreatePurchaseOrderRequest(supplierId, branchId, [new CreatePurchaseOrderLineRequest(item.Id, 10m, 5m)]));
        var purchaseOrder = await createResponse.Content.ReadFromJsonAsync<PurchaseOrderDto>(JsonOptions);
        _ = await client.PostAsync($"/purchase-orders/{purchaseOrder!.Id}/mark-sent", null);

        var response = await ReceiveAsync(
            client, purchaseOrder.Id, supplierId, branchId, item.Id, 10m, ReceivingCondition.NotGood, ReceivingRemark.Rejected);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var orders = await client.GetFromJsonAsync<List<PurchaseOrderDto>>("/purchase-orders", JsonOptions);
        Assert.Equal(0m, orders!.Single(o => o.Id == purchaseOrder.Id).Lines.Single().QuantityReceived);
        var items = await client.GetFromJsonAsync<List<ItemDto>>("/items", JsonOptions);
        Assert.Equal(0m, items!.Single(i => i.Id == item.Id).StockOnHand);
    }

    [Fact]
    public async Task Updating_a_supplier_saves_details_and_contacts()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var supplierId = await CreateSupplierAsync(client);

        var response = await client.PutAsJsonAsync(
            $"/suppliers/{supplierId}",
            new UpdateSupplierRequest(
                "Acme Foods",
                "Dry goods",
                "12 Rizal St",
                null,
                "Pays in 30 days",
                [new SupplierContactDto("Ana", ["Call", "Viber"], ["0917 111 2222", "0918 333 4444"], ["ana@acme.test"])]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await client.GetFromJsonAsync<List<SupplierDto>>("/suppliers", JsonOptions);
        var saved = list!.Single(s => s.Id == supplierId);
        Assert.Equal("Dry goods", saved.Specialization);
        Assert.Null(saved.Tin);
        var contact = Assert.Single(saved.Contacts);
        Assert.Equal(2, contact.Numbers.Count);
        Assert.Contains("Viber", contact.Modes);
    }

    private static Task<HttpResponseMessage> ReceiveAsync(
        HttpClient client,
        Guid? purchaseOrderId,
        Guid supplierId,
        Guid branchId,
        Guid itemId,
        decimal quantity,
        ReceivingCondition condition = ReceivingCondition.Good,
        ReceivingRemark remark = ReceivingRemark.Accepted)
    {
        return client.PostAsJsonAsync(
            "/incoming-receiving",
            new CreateIncomingReceivingRequest(
                purchaseOrderId,
                supplierId,
                branchId,
                DateOnly.FromDateTime(DateTime.UtcNow),
                null,
                [new CreateIncomingReceivingLineRequest(itemId, quantity, "pc", 5m, condition, remark)]));
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

    private static Task<HttpClient> AuthenticatedAdminClientAsync(PurchApiFactory factory) => TestSessions.AdminClientAsync(factory);

}
