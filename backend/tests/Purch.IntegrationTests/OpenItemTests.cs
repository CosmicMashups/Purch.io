using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
using Purch.Application.Catalog;
using Purch.Application.Onboarding;
using Purch.Application.Pos;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class OpenItemTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static object Open(string name, decimal price, decimal quantity = 1m, string? note = null)
        => new { name, unitPrice = price, quantity, note };

    private static async Task<TransactionDto> ReadAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions))!;
    }

    private static async Task<ItemDto> CreateItemAsync(HttpClient client, string name, decimal price)
    {
        var response = await client.PostAsJsonAsync("/items", new CreateItemRequest(name, null, null, null, price, null, PricingType.Unit));
        Assert.True(response.IsSuccessStatusCode);
        return (await response.Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;
    }

    private async Task<T> ScalarAsync<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            _ = command.Parameters.AddWithValue(name, value);
        }

        return (T)(await command.ExecuteScalarAsync())!;
    }

    [Fact]
    public async Task An_open_item_becomes_a_line_with_no_catalog_item_no_cost_and_its_name_on_the_dto()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        using var cashier = await TestSessions.CashierClientAsync(admin);

        var cart = await ReadAsync(await cashier.PostAsJsonAsync("/transactions/cart/lines/open", Open("Birthday cake", 450m, 2m, "No candles")));

        var line = Assert.Single(cart.Lines);
        Assert.Equal(Guid.Empty, line.ItemId);
        Assert.Equal("Birthday cake", line.ItemName);
        Assert.True(line.IsOpenItem);
        Assert.Equal("Birthday cake", line.OpenItemName);
        Assert.Equal("No candles", line.Note);
        Assert.Equal(900m, line.LineTotal);
        Assert.Equal(900m, cart.TotalAmount);
        Assert.IsType<DBNull>(await ScalarAsync<object>("SELECT \"UnitCost\" FROM \"TransactionLines\" WHERE \"Id\" = @id", ("id", line.Id)));
    }

    [Fact]
    public async Task Two_adds_make_two_lines_and_each_is_audited_with_name_price_and_quantity()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);

        _ = await ReadAsync(await admin.PostAsJsonAsync("/transactions/cart/lines/open", Open("Repair fee", 100m)));
        var cart = await ReadAsync(await admin.PostAsJsonAsync("/transactions/cart/lines/open", Open("Repair fee", 100m)));

        Assert.Equal(2, cart.Lines.Count);
        var audits = await ScalarAsync<long>(
            "SELECT COUNT(*) FROM \"AuditLogs\" WHERE \"TargetEntityId\" = @id AND \"ActionType\" = @type AND \"AfterStateJson\" LIKE '%Repair fee%'",
            ("id", cart.Id), ("type", (int)AuditActionType.OpenItemAdded));
        Assert.Equal(2L, audits);
    }

    [Fact]
    public async Task Checkout_with_an_open_item_and_a_stocked_item_only_moves_the_stocked_item()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        var item = await CreateItemAsync(admin, "Rice", 100m);
        var before = await ScalarAsync<decimal>("SELECT \"StockOnHand\" FROM \"Items\" WHERE \"Id\" = @id", ("id", item.Id));
        _ = await admin.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(item.Id, null, 1m));
        _ = await ReadAsync(await admin.PostAsJsonAsync("/transactions/cart/lines/open", Open("Gift wrap", 50m)));

        var paid = await ReadAsync(await admin.PostAsJsonAsync("/transactions/cart/payments", new RecordPaymentRequest(PaymentMethod.Cash, 150m)));

        Assert.Equal(TransactionStatus.Completed, paid.Status);
        Assert.Equal(150m, paid.TotalAmount);
        Assert.Contains(paid.Lines, l => l.IsOpenItem && l.ItemName == "Gift wrap");
        var after = await ScalarAsync<decimal>("SELECT \"StockOnHand\" FROM \"Items\" WHERE \"Id\" = @id", ("id", item.Id));
        Assert.Equal(before - 1m, after);
        var movements = await ScalarAsync<long>("SELECT COUNT(*) FROM \"InventoryMovements\" WHERE \"TenantId\" = (SELECT \"TenantId\" FROM \"Transactions\" WHERE \"Id\" = @id) AND \"Type\" = @t", ("id", paid.Id), ("t", (int)MovementType.Sale));
        Assert.Equal(1L, movements);
        var vatable = await ScalarAsync<decimal>("SELECT \"VatableSales\" + \"VatAmount\" FROM \"Transactions\" WHERE \"Id\" = @id", ("id", paid.Id));
        Assert.Equal(150m, vatable);
    }

    [Fact]
    public async Task A_sale_made_only_of_an_open_item_can_be_fully_refunded_and_restocks_nothing()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        _ = await ReadAsync(await admin.PostAsJsonAsync("/transactions/cart/lines/open", Open("Custom order", 300m)));
        var sale = await ReadAsync(await admin.PostAsJsonAsync("/transactions/cart/payments", new RecordPaymentRequest(PaymentMethod.Cash, 300m)));

        var refund = await admin.PostAsJsonAsync($"/transactions/{sale.Id}/refund", new RefundTransactionRequest("Customer cancelled", TestSessions.AdminPin));

        var refunded = await ReadAsync(refund);
        Assert.Equal(TransactionStatus.Refunded, refunded.Status);
        Assert.Equal("Custom order", refunded.Lines[0].ItemName);
        var returns = await ScalarAsync<long>("SELECT COUNT(*) FROM \"InventoryMovements\" WHERE \"TenantId\" = (SELECT \"TenantId\" FROM \"Transactions\" WHERE \"Id\" = @id) AND \"Type\" = @t",
            ("id", sale.Id), ("t", (int)MovementType.ForReturn));
        Assert.Equal(0L, returns);
    }

    [Fact]
    public async Task Returning_an_open_item_line_in_an_exchange_is_a_clear_400()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        var replacement = await CreateItemAsync(admin, "Rice", 100m);
        var line = (await ReadAsync(await admin.PostAsJsonAsync("/transactions/cart/lines/open", Open("Custom order", 300m)))).Lines[0];
        var sale = await ReadAsync(await admin.PostAsJsonAsync("/transactions/cart/payments", new RecordPaymentRequest(PaymentMethod.Cash, 300m)));

        var response = await admin.PostAsJsonAsync($"/transactions/{sale.Id}/exchange", new CreateExchangeRequest(
            [new ReturnLineRequest(line.Id, 1m)],
            [new ReplacementLineRequest(replacement.Id, null, 1m)],
            "Wrong",
            TestSessions.AdminPin,
            PaymentMethod.Cash,
            0m));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("open item", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_manager_can_discount_and_override_the_price_of_an_open_item_line()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        var lineId = (await ReadAsync(await admin.PostAsJsonAsync("/transactions/cart/lines/open", Open("Custom order", 200m)))).Lines[0].Id;

        var discounted = await ReadAsync(await admin.PutAsJsonAsync($"/transactions/cart/lines/{lineId}/discount", new { kind = "Percent", value = 10m, reason = "Regular" }));
        Assert.Equal(20m, discounted.Lines[0].ManualDiscountAmount);
        Assert.Equal(180m, discounted.TotalAmount);

        var overridden = await ReadAsync(await admin.PutAsJsonAsync($"/transactions/cart/lines/{lineId}/price", new { newUnitPrice = 150m, reason = "Quote" }));
        Assert.Equal(150m, overridden.Lines[0].UnitPrice);
        Assert.Equal(135m, overridden.TotalAmount);
        Assert.True(overridden.Lines[0].IsOpenItem);
    }

    [Fact]
    public async Task A_line_note_can_be_set_changed_and_cleared()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        var item = await CreateItemAsync(admin, "Latte", 120m);
        var lineId = (await ReadAsync(await admin.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(item.Id, null, 1m)))).Lines[0].Id;

        var set = await ReadAsync(await admin.PutAsJsonAsync($"/transactions/cart/lines/{lineId}/note", new { note = "Extra hot" }));
        Assert.Equal("Extra hot", set.Lines[0].Note);

        var cleared = await ReadAsync(await admin.PutAsJsonAsync($"/transactions/cart/lines/{lineId}/note", new { note = "  " }));
        Assert.Null(cleared.Lines[0].Note);

        var tooLong = await admin.PutAsJsonAsync($"/transactions/cart/lines/{lineId}/note", new { note = new string('x', 201) });
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
    }

    [Fact]
    public async Task Invalid_open_items_are_a_400()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/transactions/cart/lines/open", Open("  ", 10m))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/transactions/cart/lines/open", Open(new string('n', 81), 10m))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/transactions/cart/lines/open", Open("Thing", 0m))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/transactions/cart/lines/open", Open("Thing", 10m, 0m))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/transactions/cart/lines/open", Open("Thing", 10m, 1m, new string('x', 201)))).StatusCode);
    }

    [Fact]
    public async Task A_tenant_with_open_items_switched_off_is_refused_with_403_and_back_on_works()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        var off = await admin.PutAsJsonAsync("/tenant/settings/barcode", new { requiresBarcodePerItem = false, openItemEnabled = false });
        var settings = (await off.Content.ReadFromJsonAsync<TenantSettingsDto>(JsonOptions))!;
        Assert.False(settings.OpenItemEnabled);

        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync("/transactions/cart/lines/open", Open("Thing", 10m))).StatusCode);

        // A plain barcode edit that does not mention the setting leaves it alone.
        _ = await admin.PutAsJsonAsync("/tenant/settings/barcode", new { requiresBarcodePerItem = true });
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync("/transactions/cart/lines/open", Open("Thing", 10m))).StatusCode);

        _ = await admin.PutAsJsonAsync("/tenant/settings/barcode", new { requiresBarcodePerItem = true, openItemEnabled = true });
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/transactions/cart/lines/open", Open("Thing", 10m))).StatusCode);
    }
}
