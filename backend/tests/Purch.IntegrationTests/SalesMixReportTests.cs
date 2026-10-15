using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Purch.Application.Catalog;
using Purch.Application.Pos;
using Purch.Application.Reporting;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class SalesMixReportTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static string Url(DateTimeOffset from, DateTimeOffset to) =>
        $"/reports/sales-mix?from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}";

    [Fact]
    public async Task Sales_mix_reports_a_cash_sale_under_cash_uncategorised_and_its_business_hour()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        var itemResponse = await client.PostAsJsonAsync(
            "/items", new CreateItemRequest("Mix Item", null, null, null, 40m, null, PricingType.Unit));
        var item = (await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;
        _ = await client.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(item.Id, null, 1m));
        _ = await client.PostAsJsonAsync("/transactions/cart/payments", new RecordPaymentRequest(PaymentMethod.Cash, 40m));

        var response = await client.GetAsync(Url(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1)));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var report = (await response.Content.ReadFromJsonAsync<SalesMixDto>(JsonOptions))!;
        Assert.Equal(40m, report.GrossSales);
        Assert.Equal(1, report.TransactionCount);
        var cash = Assert.Single(report.Payments);
        Assert.Equal(PaymentMethod.Cash, cash.Method);
        Assert.Equal(40m, cash.Amount);
        Assert.False(cash.IsCredit);
        var category = Assert.Single(report.Categories);
        Assert.Equal("Uncategorised", category.Name);
        Assert.Equal(40m, category.Revenue);
        Assert.Equal(24, report.Hours.Count);
        var businessHour = DateTimeOffset.UtcNow.ToOffset(ReportTimeZone.Offset).Hour;
        Assert.Equal(40m, report.Hours.Single(h => h.Hour == businessHour).Sales);
        Assert.Equal(40m, report.Hours.Sum(h => h.Sales));
    }

    [Fact]
    public async Task Sales_mix_refuses_a_range_over_366_days()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);

        var response = await client.GetAsync(Url(DateTimeOffset.UtcNow.AddDays(-400), DateTimeOffset.UtcNow));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_cashier_cannot_read_sales_mix()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        using var cashier = await TestSessions.CashierClientAsync(admin, "678912", "Cal Cashier");

        var response = await cashier.GetAsync(Url(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1)));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
