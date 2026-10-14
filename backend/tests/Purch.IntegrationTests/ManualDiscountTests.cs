using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
using Purch.Application.Catalog;
using Purch.Application.Pos;
using Purch.Application.Promotions;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class ManualDiscountTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static async Task<ItemDto> CreateItemAsync(HttpClient client, string name, decimal price)
    {
        var response = await client.PostAsJsonAsync("/items", new CreateItemRequest(name, null, null, null, price, null, PricingType.Unit));
        Assert.True(response.IsSuccessStatusCode);
        return (await response.Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;
    }

    private static async Task<TransactionDto> AddAsync(HttpClient client, ItemDto item, decimal quantity = 1m)
    {
        var response = await client.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(item.Id, null, quantity));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions))!;
    }

    private static async Task<TransactionDto> ReadAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions))!;
    }

    private async Task<List<string>> AuditAfterStatesAsync(Guid transactionId, AuditActionType type)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT \"AfterStateJson\" FROM \"AuditLogs\" WHERE \"TargetEntityId\" = @id AND \"ActionType\" = @type", connection);
        _ = command.Parameters.AddWithValue("id", transactionId);
        _ = command.Parameters.AddWithValue("type", (int)type);
        var rows = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(reader.GetString(0));
        }

        return rows;
    }

    private static object Discount(string kind, decimal value, string reason = "Damaged box") => new { kind, value, reason };

    [Fact]
    public async Task A_cashier_is_refused_every_manual_discount_and_price_route()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        using var cashier = await TestSessions.CashierClientAsync(admin);
        var item = await CreateItemAsync(admin, "Rice", 200m);
        var cart = await AddAsync(cashier, item);
        var lineId = cart.Lines[0].Id;

        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.PutAsJsonAsync($"/transactions/cart/lines/{lineId}/discount", Discount("Percent", 10m))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.PutAsJsonAsync("/transactions/cart/discount", Discount("Percent", 10m))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.PutAsJsonAsync($"/transactions/cart/lines/{lineId}/price", new { newUnitPrice = 150m, reason = "Price match" })).StatusCode);
    }

    [Fact]
    public async Task A_manager_discounts_a_line_and_the_audit_row_carries_the_reason()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        using var manager = await TestSessions.ManagerClientAsync(admin);
        var item = await CreateItemAsync(admin, "Rice", 200m);
        var cart = await AddAsync(manager, item);
        var lineId = cart.Lines[0].Id;

        var priced = await ReadAsync(await manager.PutAsJsonAsync($"/transactions/cart/lines/{lineId}/discount", Discount("Percent", 10m, "Dented can")));

        Assert.Equal(20m, priced.Lines[0].ManualDiscountAmount);
        Assert.Equal(180m, priced.TotalAmount);
        Assert.Equal(20m, priced.DiscountAmount);
        var audits = await AuditAfterStatesAsync(priced.Id, AuditActionType.DiscountOverride);
        Assert.Contains(audits, json => json.Contains("Dented can"));
    }

    [Fact]
    public async Task A_fixed_discount_larger_than_the_line_is_clamped_and_zero_clears_it()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        var item = await CreateItemAsync(admin, "Rice", 200m);
        var lineId = (await AddAsync(admin, item)).Lines[0].Id;

        var clamped = await ReadAsync(await admin.PutAsJsonAsync($"/transactions/cart/lines/{lineId}/discount", Discount("Fixed", 500m)));
        Assert.Equal(200m, clamped.Lines[0].ManualDiscountAmount);
        Assert.Equal(0m, clamped.TotalAmount);

        var cleared = await ReadAsync(await admin.PutAsJsonAsync($"/transactions/cart/lines/{lineId}/discount", Discount("Fixed", 0m)));
        Assert.Equal(0m, cleared.Lines[0].ManualDiscountAmount);
        Assert.Equal(200m, cleared.TotalAmount);
    }

    [Fact]
    public async Task A_cart_discount_applies_after_line_discounts_and_an_empty_reason_is_a_400()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        var rice = await CreateItemAsync(admin, "Rice", 200m);
        var oil = await CreateItemAsync(admin, "Oil", 300m);
        _ = await AddAsync(admin, rice);
        var cart = await AddAsync(admin, oil);
        var riceLine = cart.Lines.Single(l => l.ItemName == "Rice").Id;

        _ = await ReadAsync(await admin.PutAsJsonAsync($"/transactions/cart/lines/{riceLine}/discount", Discount("Fixed", 50m)));
        var priced = await ReadAsync(await admin.PutAsJsonAsync("/transactions/cart/discount", Discount("Percent", 10m)));

        Assert.Equal(405m, priced.TotalAmount);
        Assert.Equal(95m, priced.DiscountAmount);
        Assert.Equal(45m, priced.ManualDiscountAmount);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/transactions/cart/discount", Discount("Percent", 5m, "  "))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/transactions/cart/lines/{riceLine}/price", new { newUnitPrice = 90m, reason = "" })).StatusCode);
    }

    [Fact]
    public async Task A_manual_cart_discount_suppresses_the_promo_code()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        var item = await CreateItemAsync(admin, "Rice", 200m);
        _ = await AddAsync(admin, item);
        var create = await admin.PostAsJsonAsync("/promo-codes", new CreatePromoCodeRequest("SAVE50", PromoDiscountType.FixedAmount, 50m, null));
        Assert.True(create.IsSuccessStatusCode, await create.Content.ReadAsStringAsync());
        var withCode = await ReadAsync(await admin.PutAsJsonAsync("/transactions/cart/promo-code", new ApplyPromoCodeRequest("SAVE50")));
        Assert.Equal(50m, withCode.PromoDiscountAmount);

        var priced = await ReadAsync(await admin.PutAsJsonAsync("/transactions/cart/discount", Discount("Fixed", 20m)));

        Assert.Equal(0m, priced.PromoDiscountAmount);
        Assert.Equal(180m, priced.TotalAmount);
    }

    [Fact]
    public async Task Senior_discount_after_a_manual_discount_clears_it_and_audits_the_removal()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        var item = await CreateItemAsync(admin, "Rice", 112m);
        var lineId = (await AddAsync(admin, item)).Lines[0].Id;
        _ = await ReadAsync(await admin.PutAsJsonAsync($"/transactions/cart/lines/{lineId}/discount", Discount("Fixed", 12m)));

        var senior = await ReadAsync(await admin.PutAsJsonAsync("/transactions/cart/senior-pwd-discount", new ApplySeniorPwdDiscountRequest(true)));

        Assert.Equal(0m, senior.Lines[0].ManualDiscountAmount);
        Assert.Equal(0m, senior.ManualDiscountAmount);
        Assert.True(senior.SeniorPwdDiscountApplied);
        Assert.Equal(80m, senior.TotalAmount);
        Assert.Equal(12m, senior.VatExemptAmount);
        var audits = await AuditAfterStatesAsync(senior.Id, AuditActionType.DiscountOverride);
        Assert.Contains(audits, json => json.Contains("manualDiscountsCleared", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task A_manual_discount_while_senior_is_applied_is_a_409()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        var item = await CreateItemAsync(admin, "Rice", 200m);
        var lineId = (await AddAsync(admin, item)).Lines[0].Id;
        _ = await ReadAsync(await admin.PutAsJsonAsync("/transactions/cart/senior-pwd-discount", new ApplySeniorPwdDiscountRequest(true)));

        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync($"/transactions/cart/lines/{lineId}/discount", Discount("Percent", 10m))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync("/transactions/cart/discount", Discount("Percent", 10m))).StatusCode);
    }

    [Fact]
    public async Task A_price_override_remembers_the_old_price_recomputes_the_line_and_is_audited()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        var item = await CreateItemAsync(admin, "Rice", 200m);
        var lineId = (await AddAsync(admin, item, 2m)).Lines[0].Id;

        var priced = await ReadAsync(await admin.PutAsJsonAsync($"/transactions/cart/lines/{lineId}/price", new { newUnitPrice = 150m, reason = "Price match" }));

        var line = priced.Lines[0];
        Assert.Equal(150m, line.UnitPrice);
        Assert.Equal(200m, line.PriceOverrideFrom);
        Assert.Equal(300m, line.LineTotal);
        Assert.Equal(300m, priced.TotalAmount);
        var audits = await AuditAfterStatesAsync(priced.Id, AuditActionType.PriceOverride);
        Assert.Contains(audits, json => json.Contains("Price match"));
    }
}
