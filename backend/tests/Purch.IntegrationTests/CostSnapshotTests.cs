using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
using Purch.Application.Catalog;
using Purch.Application.Inventory;
using Purch.Application.Onboarding;
using Purch.Application.Pos;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class CostSnapshotTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static async Task<ItemDto> CreateItemAsync(HttpClient client, string name, decimal? cost)
    {
        var response = await client.PostAsJsonAsync("/items", new CreateItemRequest(name, null, null, null, 100m, null, PricingType.Unit, CostPrice: cost));
        Assert.True(response.IsSuccessStatusCode);
        return (await response.Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;
    }

    private static async Task<TransactionDto> CheckoutAsync(HttpClient client, Guid itemId, decimal quantity = 1m)
    {
        var response = await client.PostAsJsonAsync(
            "/transactions/checkout",
            new CheckoutRequest(
                Guid.NewGuid(),
                [new AddTransactionLineRequest(itemId, null, quantity)],
                SeniorPwdDiscountApplied: false,
                PromoCode: null,
                OrderType: "Take Out",
                Payment: new RecordPaymentRequest(PaymentMethod.Cash, 100000m),
                ExpectedTotal: 100m * quantity));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions))!;
    }

    private async Task<List<decimal?>> LineCostsAsync(Guid transactionId)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT \"UnitCost\" FROM \"TransactionLines\" WHERE \"TransactionId\" = @id", connection);
        command.Parameters.AddWithValue("id", transactionId);
        await using var reader = await command.ExecuteReaderAsync();
        var costs = new List<decimal?>();
        while (await reader.ReadAsync())
        {
            costs.Add(reader.IsDBNull(0) ? null : reader.GetDecimal(0));
        }

        return costs;
    }

    [Fact]
    public async Task Line_snapshots_item_cost_and_later_cost_change_does_not_touch_it()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        var item = await CreateItemAsync(client, "Snap Item", 40m);
        Assert.Equal(40m, item.CostPrice);

        var sale = await CheckoutAsync(client, item.Id);

        var update = await client.PutAsJsonAsync($"/items/{item.Id}", new UpdateItemRequest("Snap Item", null, null, null, 100m, null, true, CostPrice: 55m));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(55m, (await update.Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!.CostPrice);
        Assert.Equal([40m], await LineCostsAsync(sale.Id));
    }

    [Fact]
    public async Task Merged_identical_lines_keep_the_snapshot()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        var item = await CreateItemAsync(client, "Merge Item", 40m);

        var response = await client.PostAsJsonAsync(
            "/transactions/checkout",
            new CheckoutRequest(
                Guid.NewGuid(),
                [new AddTransactionLineRequest(item.Id, null, 1m), new AddTransactionLineRequest(item.Id, null, 2m)],
                SeniorPwdDiscountApplied: false,
                PromoCode: null,
                OrderType: "Take Out",
                Payment: new RecordPaymentRequest(PaymentMethod.Cash, 1000m),
                ExpectedTotal: 300m));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sale = (await response.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions))!;

        Assert.Equal([40m], await LineCostsAsync(sale.Id));
    }

    [Fact]
    public async Task Item_without_cost_snapshots_null()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        var item = await CreateItemAsync(client, "No Cost", null);
        Assert.Null(item.CostPrice);

        var sale = await CheckoutAsync(client, item.Id);

        Assert.Equal([(decimal?)null], await LineCostsAsync(sale.Id));
    }

    [Fact]
    public async Task Recipe_item_snapshots_sum_of_ingredient_cost_times_quantity()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        _ = await client.PutAsJsonAsync("/tenant/settings/inventory-tracking", new UpdateInventoryTrackingSettingRequest(true));
        var a = (await (await client.PostAsJsonAsync("/inventory-items", new CreateInventoryItemRequest("Beans", null, "g", "g", 1m, null, UnitCost: 10m))).Content.ReadFromJsonAsync<InventoryItemDto>(JsonOptions))!;
        var b = (await (await client.PostAsJsonAsync("/inventory-items", new CreateInventoryItemRequest("Milk", null, "ml", "ml", 1m, null, UnitCost: 5m))).Content.ReadFromJsonAsync<InventoryItemDto>(JsonOptions))!;
        var c = (await (await client.PostAsJsonAsync("/inventory-items", new CreateInventoryItemRequest("Napkin", null, "pc", "pc", 1m, null))).Content.ReadFromJsonAsync<InventoryItemDto>(JsonOptions))!;
        Assert.Equal(10m, a.UnitCost);

        var recipeItem = await CreateItemAsync(client, "Latte", null);
        var put = await client.PutAsJsonAsync($"/items/{recipeItem.Id}/recipe", new ReplaceItemRecipeRequest([new ReplaceItemRecipeLineRequest(a.Id, 2m), new ReplaceItemRecipeLineRequest(b.Id, 1m)]));
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var sale = await CheckoutAsync(client, recipeItem.Id);
        Assert.Equal([25m], await LineCostsAsync(sale.Id));

        var unknown = await CreateItemAsync(client, "Mystery", null);
        _ = await client.PutAsJsonAsync($"/items/{unknown.Id}/recipe", new ReplaceItemRecipeRequest([new ReplaceItemRecipeLineRequest(a.Id, 1m), new ReplaceItemRecipeLineRequest(c.Id, 1m)]));
        var sale2 = await CheckoutAsync(client, unknown.Id);
        Assert.Equal([(decimal?)null], await LineCostsAsync(sale2.Id));
    }

    [Fact]
    public async Task Negative_cost_is_rejected_on_create_and_update()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);

        var create = await client.PostAsJsonAsync("/items", new CreateItemRequest("Neg", null, null, null, 10m, null, PricingType.Unit, CostPrice: -1m));
        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);

        var item = await CreateItemAsync(client, "Ok", 5m);
        var update = await client.PutAsJsonAsync($"/items/{item.Id}", new UpdateItemRequest("Ok", null, null, null, 10m, null, true, CostPrice: -2m));
        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
    }

    private async Task<decimal?> ReceiveAsync(bool updateCost)
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        var settings = await client.PutAsJsonAsync("/tenant/settings/inventory-tracking", new UpdateInventoryTrackingSettingRequest(false, updateCost));
        Assert.Equal(HttpStatusCode.OK, settings.StatusCode);
        Assert.Equal(updateCost, (await settings.Content.ReadFromJsonAsync<TenantSettingsDto>(JsonOptions))!.UpdateCostOnReceiving);

        var branchId = (await client.GetFromJsonAsync<List<BranchDto>>("/branches", JsonOptions))!.Single().Id;
        var item = await CreateItemAsync(client, "Recv Item", 40m);
        var supplier = (await (await client.PostAsJsonAsync("/suppliers", new CreateSupplierRequest("Acme", null))).Content.ReadFromJsonAsync<SupplierDto>(JsonOptions))!;
        var receive = await client.PostAsJsonAsync(
            "/incoming-receiving",
            new CreateIncomingReceivingRequest(
                null,
                supplier.Id,
                branchId,
                DateOnly.FromDateTime(DateTime.UtcNow),
                null,
                [new CreateIncomingReceivingLineRequest(item.Id, 10m, "pc", 47.5m, ReceivingCondition.Good, ReceivingRemark.Accepted)]));
        Assert.Equal(HttpStatusCode.OK, receive.StatusCode);

        return (await client.GetFromJsonAsync<List<ItemDto>>("/items", JsonOptions))!.Single(i => i.Id == item.Id).CostPrice;
    }

    [Fact]
    public async Task Receiving_updates_item_cost_when_setting_on()
    {
        Assert.Equal(47.5m, await ReceiveAsync(true));
    }

    [Fact]
    public async Task Receiving_leaves_item_cost_when_setting_off()
    {
        Assert.Equal(40m, await ReceiveAsync(false));
    }
}
