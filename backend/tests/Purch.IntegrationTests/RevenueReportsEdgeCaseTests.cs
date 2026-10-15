using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
using Purch.Application.Catalog;
using Purch.Application.Pos;
using Purch.Application.Reporting;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class RevenueReportsEdgeCaseTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static string Query(string path, int fromDays = -4) =>
        $"{path}?from={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(fromDays).ToString("O"))}&to={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(1).ToString("O"))}";

    private static async Task<ItemDto> CreateItemAsync(HttpClient client, string name, decimal price, decimal? cost = null)
    {
        var response = await client.PostAsJsonAsync("/items", new CreateItemRequest(name, null, null, null, price, null, PricingType.Unit, CostPrice: cost));
        return (await response.Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string path, int fromDays = -4)
    {
        var response = await client.GetAsync(Query(path, fromDays));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }

    private async Task ExecAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection) { CommandTimeout = 240 };
        foreach (var (name, value) in parameters)
        {
            _ = command.Parameters.AddWithValue(name, value);
        }

        _ = await command.ExecuteNonQueryAsync();
    }

    private static async Task<TransactionDto> PayAsync(HttpClient client, ItemDto item, RecordPaymentRequest payment)
    {
        _ = await client.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(item.Id, null, 1m));
        var response = await client.PostAsJsonAsync("/transactions/cart/payments", payment);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions))!;
    }

    [Fact]
    public async Task A_split_sale_is_counted_per_tender_and_cash_is_net_of_change()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        var item = await CreateItemAsync(client, "Split item", 150m);
        _ = await PayAsync(client, item, new RecordPaymentRequest(
            PaymentMethod.Cash,
            null,
            Parts:
            [
                new PaymentPartRequest(PaymentMethod.Cash, 100m, 200m, null, null),
                new PaymentPartRequest(PaymentMethod.ManualGcashQr, 50m, null, null, "GC-1"),
            ]));

        var mix = await GetAsync<SalesMixDto>(client, "/reports/sales-mix");

        Assert.Equal(150m, mix.GrossSales);
        Assert.Equal(100m, mix.Payments.Single(p => p.Method == PaymentMethod.Cash).Amount);
        Assert.Equal(50m, mix.Payments.Single(p => p.Method == PaymentMethod.ManualGcashQr).Amount);
        Assert.Equal(150m, mix.Payments.Sum(p => p.Amount));
    }

    [Fact]
    public async Task A_senior_pwd_sale_is_reported_as_exempt_with_its_own_discount_line()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        var item = await CreateItemAsync(client, "Senior item", 112m);
        _ = await client.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(item.Id, null, 1m));
        var priced = await client.PutAsJsonAsync("/transactions/cart/senior-pwd-discount", new ApplySeniorPwdDiscountRequest(true));
        var cart = (await priced.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions))!;
        _ = await client.PostAsJsonAsync("/transactions/cart/payments", new RecordPaymentRequest(PaymentMethod.Cash, cart.TotalAmount));

        var tax = await GetAsync<TaxReportDto>(client, "/reports/tax");
        var discounts = await GetAsync<DiscountReportDto>(client, "/reports/discounts");

        Assert.Equal(1, tax.SeniorPwdSaleCount);
        Assert.True(tax.Totals.VatExemptSales > 0m);
        Assert.True(discounts.SeniorPwdDiscount > 0m);
        Assert.Equal(0m, discounts.ManualDiscount);
        Assert.Equal(discounts.SeniorPwdDiscount, discounts.TotalDiscounts);
    }

    [Fact]
    public async Task Sales_on_different_business_days_are_reported_as_separate_days()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        var older = await PayAsync(client, await CreateItemAsync(client, "Older", 40m), new RecordPaymentRequest(PaymentMethod.Cash, 40m));
        _ = await PayAsync(client, await CreateItemAsync(client, "Newer", 60m), new RecordPaymentRequest(PaymentMethod.Cash, 60m));
        await ExecAsync("UPDATE \"Transactions\" SET \"CompletedAt\" = \"CompletedAt\" - interval '2 days' WHERE \"Id\" = @id", ("id", older.Id));

        var tax = await GetAsync<TaxReportDto>(client, "/reports/tax");
        var mix = await GetAsync<SalesMixDto>(client, "/reports/sales-mix");

        Assert.Equal(2, tax.Days.Count);
        Assert.Equal(100m, tax.Totals.GrossSales);
        Assert.Equal(2, mix.OperatingDays);
        Assert.Equal(100m, mix.Hours.Sum(h => h.Sales));
    }

    [Fact]
    public async Task Empty_discards_and_expired_parked_carts_are_counted_apart_from_voids()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        var counted = await CreateItemAsync(client, "Counted void", 30m);
        var expired = await CreateItemAsync(client, "Expired parked", 20m);
        var empty = await CreateItemAsync(client, "Empty discard", 10m);

        async Task<Guid> VoidCartAsync(ItemDto item)
        {
            var added = await client.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(item.Id, null, 1m));
            var cart = (await added.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions))!;
            _ = await client.PostAsJsonAsync("/transactions/cart/void", new VoidCartRequest(TestSessions.AdminPin, "test"));
            return cart.Id;
        }

        _ = await VoidCartAsync(counted);
        var expiredId = await VoidCartAsync(expired);
        var emptyId = await VoidCartAsync(empty);
        await ExecAsync("UPDATE \"Transactions\" SET \"ParkedAt\" = now() WHERE \"Id\" = @id", ("id", expiredId));
        await ExecAsync("UPDATE \"Transactions\" SET \"TotalAmount\" = 0 WHERE \"Id\" = @id", ("id", emptyId));

        var report = await GetAsync<VoidReportDto>(client, "/reports/voids");

        Assert.Equal(1, report.Count);
        Assert.Equal(30m, report.Amount);
        Assert.Equal(1, report.EmptyDiscardedCount);
        Assert.Equal(1, report.ExpiredParkedCount);
        Assert.Single(report.Voids.Rows);
    }

    [Fact]
    public async Task A_manager_can_read_the_void_report_but_not_download_the_listing_csv()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        using var manager = await TestSessions.ManagerClientAsync(admin);

        var report = await manager.GetAsync(Query("/reports/voids"));
        var csv = await manager.GetAsync(Query("/reports/refunds.csv"));

        Assert.Equal(HttpStatusCode.OK, report.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, csv.StatusCode);
    }

    [Fact]
    public async Task The_aggregate_reports_stay_fast_over_twenty_thousand_sales()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        var item = await CreateItemAsync(client, "Seed source", 50m, cost: 20m);
        var sale = await PayAsync(client, item, new RecordPaymentRequest(PaymentMethod.Cash, 50m));

        // Clone the one real sale (and its line and payment) 20,000 times across the last 60 days.
        await ExecAsync(
            """
            CREATE TEMP TABLE clones AS
            SELECT gen_random_uuid() AS new_id, g AS n FROM generate_series(1, 20000) g;

            INSERT INTO "Transactions"
            SELECT (jsonb_populate_record(NULL::"Transactions",
                to_jsonb(t) || jsonb_build_object(
                    'Id', c.new_id,
                    'ReceiptNumber', NULL,
                    'ClientSaleId', NULL,
                    'ZReadingNumber', NULL,
                    'CompletedAt', now() - (c.n % 60) * interval '1 day' - (c.n % 24) * interval '1 hour'))).*
            FROM "Transactions" t, clones c WHERE t."Id" = @id;

            INSERT INTO "TransactionLines"
            SELECT (jsonb_populate_record(NULL::"TransactionLines",
                to_jsonb(l) || jsonb_build_object('Id', gen_random_uuid(), 'TransactionId', c.new_id))).*
            FROM "TransactionLines" l, clones c WHERE l."TransactionId" = @id;

            INSERT INTO "Payments"
            SELECT (jsonb_populate_record(NULL::"Payments",
                to_jsonb(p) || jsonb_build_object('Id', gen_random_uuid(), 'TransactionId', c.new_id))).*
            FROM "Payments" p, clones c WHERE p."TransactionId" = @id;
            """,
            ("id", sale.Id));

        var budget = TimeSpan.FromSeconds(6);
        foreach (var path in new[] { "/reports/sales-mix", "/reports/tax", "/reports/gross-profit", "/reports/discounts", "/reports/dashboard-insights" })
        {
            var watch = Stopwatch.StartNew();
            var response = await client.GetAsync(Query(path, -90));
            watch.Stop();

            Assert.True(response.IsSuccessStatusCode, $"{path}: {await response.Content.ReadAsStringAsync()}");
            Assert.True(watch.Elapsed < budget, $"{path} took {watch.Elapsed.TotalSeconds:F1}s over 20,001 sales");
        }

        var mix = await GetAsync<SalesMixDto>(client, "/reports/sales-mix", -90);
        Assert.Equal(20_001, mix.TransactionCount);
    }
}
