using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Purch.Application.Catalog;
using Purch.Application.Inventory;
using Purch.Application.Onboarding;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class PurchaseRequestEndpointsTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Out_of_stock_items_are_suggested_first_with_their_supplier_and_a_suggested_quantity()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        var supplier = await CreateSupplierAsync(client, "Metro Wholesale");
        var low = await CreateItemAsync(client, "Sugar", threshold: 10m, stockIn: 4m);
        var empty = await CreateItemAsync(client, "Salt", threshold: 5m, stockIn: 0m);

        var set = await client.PutAsJsonAsync($"/items/{empty.Id}/suppliers", new SetItemSuppliersRequest([new SetItemSupplierEntry(supplier.Id, false, 12m)]));
        Assert.Equal(HttpStatusCode.OK, set.StatusCode);
        var links = (await set.Content.ReadFromJsonAsync<List<SupplierLinkDto>>(JsonOptions))!;
        Assert.True(Assert.Single(links).IsPreferred, "a lone supplier is the preferred one");

        var rows = (await client.GetFromJsonAsync<List<RestockSuggestionDto>>("/inventory/restock-suggestions", JsonOptions))!;

        Assert.Equal([empty.Id, low.Id], rows.Select(row => row.ItemId!.Value));
        Assert.Equal(5m, rows[0].SuggestedQuantity);
        Assert.Equal("Metro Wholesale", Assert.Single(rows[0].Suppliers).SupplierName);
        Assert.Equal(6m, rows[1].SuggestedQuantity);
        Assert.Null(rows[0].Pending);
    }

    [Fact]
    public async Task A_request_carries_a_branch_control_number_and_marks_its_items_as_pending()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        var branchId = await MainBranchIdAsync(client);
        var item = await CreateItemAsync(client, "Flour", threshold: 8m, stockIn: 0m);

        var created = await CreateRequestAsync(client, branchId, item.Id, 8m);
        var second = await CreateRequestAsync(client, branchId, item.Id, 2m);

        var branch = (await client.GetFromJsonAsync<List<BranchDto>>("/branches", JsonOptions))!.Single();
        Assert.False(string.IsNullOrWhiteSpace(branch.Code));
        Assert.Equal($"{branch.Code}-PR-00001", created.ControlNumber);
        Assert.Equal($"{branch.Code}-PR-00002", second.ControlNumber);
        Assert.Equal(PurchaseRequestStatus.Open, created.Status);
        Assert.Equal("pc", Assert.Single(created.Lines).Unit);

        var rows = (await client.GetFromJsonAsync<List<RestockSuggestionDto>>("/inventory/restock-suggestions", JsonOptions))!;
        Assert.Equal(second.ControlNumber, Assert.Single(rows, row => row.ItemId == item.Id).Pending);
    }

    [Fact]
    public async Task An_unapproved_request_cannot_be_converted_and_an_approved_one_makes_a_purchase_order_per_supplier()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        var branchId = await MainBranchIdAsync(client);
        var supplierA = await CreateSupplierAsync(client, "Alpha Foods", "Alpha Foods Inc", "001-234");
        var supplierB = await CreateSupplierAsync(client, "Beta Trading");
        var rice = await CreateItemAsync(client, "Rice", threshold: 10m, stockIn: 0m);
        var oil = await CreateItemAsync(client, "Oil", threshold: 10m, stockIn: 0m);

        var request = await CreateRequestAsync(client, branchId, rice.Id, 10m, oil.Id, 4m);
        var riceLine = request.Lines.Single(line => line.ItemId == rice.Id);
        var oilLine = request.Lines.Single(line => line.ItemId == oil.Id);
        var body = new ConvertPurchaseRequestRequest(
        [
            new ConvertPurchaseRequestOrder(supplierA.Id, [riceLine.Id], VatInclusive: false, DiscountAmount: 10m),
            new ConvertPurchaseRequestOrder(supplierB.Id, [oilLine.Id], VatInclusive: true),
        ]);

        var early = await client.PostAsJsonAsync($"/purchase-requests/{request.Id}/convert", body);
        Assert.Equal(HttpStatusCode.BadRequest, early.StatusCode);

        var approved = await client.PostAsync($"/purchase-requests/{request.Id}/approve", null);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);

        var converted = await client.PostAsJsonAsync($"/purchase-requests/{request.Id}/convert", body);
        Assert.Equal(HttpStatusCode.OK, converted.StatusCode);
        var orders = (await converted.Content.ReadFromJsonAsync<List<PurchaseOrderDto>>(JsonOptions))!;
        Assert.Equal(2, orders.Count);
        Assert.All(orders, order => Assert.Equal(PurchaseOrderStatus.Draft, order.Status));
        Assert.All(orders, order => Assert.Equal(request.Id, order.PurchaseRequestId));
        Assert.Equal(2, orders.Select(order => order.ControlNumber).Distinct().Count());

        var orderA = orders.Single(order => order.SupplierId == supplierA.Id);
        Assert.Equal("Alpha Foods Inc", orderA.PaymentAccountName);
        Assert.Equal("001-234", orderA.PaymentAccountNumber);
        Assert.Equal(10m, Assert.Single(orderA.Lines).QuantityOrdered);

        var refreshed = (await client.GetFromJsonAsync<List<PurchaseRequestDto>>("/purchase-requests", JsonOptions))!.Single(r => r.Id == request.Id);
        Assert.Equal(PurchaseRequestStatus.Converted, refreshed.Status);
    }

    [Fact]
    public async Task Order_totals_follow_the_vat_choice_and_the_discount()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        var branchId = await MainBranchIdAsync(client);
        var supplier = await CreateSupplierAsync(client, "Gamma Supply");
        var item = await CreateItemAsync(client, "Noodles", threshold: 5m, stockIn: 0m);

        var exclusive = await PostOrderAsync(client, supplier.Id, branchId, item.Id, vatInclusive: false);
        Assert.Equal(1000m, exclusive.Subtotal);
        Assert.Equal(100m, exclusive.DiscountAmount);
        Assert.Equal(108m, exclusive.VatAmount);
        Assert.Equal(1008m, exclusive.OrderTotal);

        var inclusive = await PostOrderAsync(client, supplier.Id, branchId, item.Id, vatInclusive: true);
        Assert.Equal(900m, inclusive.OrderTotal);
        Assert.Equal(96.43m, inclusive.VatAmount);
    }

    [Fact]
    public async Task Only_an_admin_or_manager_can_approve_a_request()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        var branchId = await MainBranchIdAsync(admin);
        var item = await CreateItemAsync(admin, "Tea", threshold: 3m, stockIn: 0m);
        var request = await CreateRequestAsync(admin, branchId, item.Id, 3m);

        using var cashier = await TestSessions.CashierClientAsync(admin);
        var denied = await cashier.PostAsync($"/purchase-requests/{request.Id}/approve", null);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        using var manager = await TestSessions.ManagerClientAsync(admin);
        var approved = await manager.PostAsync($"/purchase-requests/{request.Id}/approve", null);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
    }

    private static async Task<PurchaseOrderDto> PostOrderAsync(HttpClient client, Guid supplierId, Guid branchId, Guid itemId, bool vatInclusive)
    {
        var response = await client.PostAsJsonAsync(
            "/purchase-orders",
            new CreatePurchaseOrderRequest(
                supplierId,
                branchId,
                [new CreatePurchaseOrderLineRequest(itemId, 10m, 100m)],
                VatInclusive: vatInclusive,
                DiscountAmount: 100m));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PurchaseOrderDto>(JsonOptions))!;
    }

    private static async Task<PurchaseRequestDto> CreateRequestAsync(HttpClient client, Guid branchId, Guid itemId, decimal quantity, Guid? secondItemId = null, decimal secondQuantity = 0m)
    {
        var lines = new List<CreatePurchaseRequestLineRequest> { new(itemId, null, quantity, null) };
        if (secondItemId is { } second)
        {
            lines.Add(new CreatePurchaseRequestLineRequest(second, null, secondQuantity, null));
        }

        var response = await client.PostAsJsonAsync("/purchase-requests", new CreatePurchaseRequestRequest(branchId, null, null, lines));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PurchaseRequestDto>(JsonOptions))!;
    }

    private static async Task<SupplierDto> CreateSupplierAsync(HttpClient client, string name, string? accountName = null, string? accountNumber = null)
    {
        var response = await client.PostAsJsonAsync(
            "/suppliers",
            new CreateSupplierRequest(name, PaymentAccountName: accountName, PaymentAccountNumber: accountNumber));
        return (await response.Content.ReadFromJsonAsync<SupplierDto>(JsonOptions))!;
    }

    private static async Task<ItemDto> CreateItemAsync(HttpClient client, string name, decimal threshold, decimal stockIn)
    {
        var response = await client.PostAsJsonAsync("/items", new CreateItemRequest(name, null, null, null, 50m, null, PricingType.Unit));
        var item = (await response.Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;
        _ = await client.PutAsJsonAsync($"/items/{item.Id}/low-stock-threshold", new UpdateLowStockThresholdRequest(threshold));
        if (stockIn > 0)
        {
            _ = await client.PostAsJsonAsync(
                "/inventory/movements",
                new RecordMovementRequest(item.Id, await MainBranchIdAsync(client), MovementType.StockIn, stockIn, null, null, null, null));
        }
        return item;
    }

    private static async Task<Guid> MainBranchIdAsync(HttpClient client)
    {
        var branches = await client.GetFromJsonAsync<List<BranchDto>>("/branches", JsonOptions);
        return branches!.First().Id;
    }
}
