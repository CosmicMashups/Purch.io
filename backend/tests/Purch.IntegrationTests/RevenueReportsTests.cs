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
public sealed class RevenueReportsTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static string Query(string path) =>
        $"{path}?from={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-1).ToString("O"))}&to={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(1).ToString("O"))}";

    private static async Task<ItemDto> CreateItemAsync(HttpClient client, string name, decimal price, decimal? cost = null)
    {
        var response = await client.PostAsJsonAsync(
            "/items", new CreateItemRequest(name, null, null, null, price, null, PricingType.Unit, CostPrice: cost));
        return (await response.Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;
    }

    private static async Task<TransactionDto> SellAsync(HttpClient client, ItemDto item, decimal quantity = 1m)
    {
        _ = await client.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(item.Id, null, quantity));
        var response = await client.PostAsJsonAsync(
            "/transactions/cart/payments", new RecordPaymentRequest(PaymentMethod.Cash, item.BasePrice * quantity));
        return (await response.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions))!;
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string path)
    {
        var response = await client.GetAsync(Query(path));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }

    [Fact]
    public async Task The_tax_report_totals_the_stamped_sales_by_business_day()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        _ = await SellAsync(client, await CreateItemAsync(client, "Taxed", 112m));

        var report = await GetAsync<TaxReportDto>(client, "/reports/tax");

        Assert.Equal(1, report.Totals.SaleCount);
        Assert.Equal(112m, report.Totals.GrossSales);
        Assert.Single(report.Days);
        Assert.Equal(report.Totals.VatableSales + report.Totals.VatAmount, report.Days[0].VatableSales + report.Days[0].VatAmount);
    }

    [Fact]
    public async Task Gross_profit_excludes_unknown_cost_lines_and_reports_the_coverage()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        _ = await SellAsync(client, await CreateItemAsync(client, "Costed", 100m, cost: 60m));
        _ = await SellAsync(client, await CreateItemAsync(client, "Uncosted", 100m));

        var report = await GetAsync<ProfitReportDto>(client, "/reports/gross-profit");

        Assert.Equal(60m, report.Cost);
        Assert.Equal(50m, report.CoveragePercent);
        Assert.True(report.UnknownCostRevenue > 0m);
        Assert.Equal(report.RevenueExVat, report.KnownCostRevenue + report.UnknownCostRevenue);
        Assert.Equal(report.KnownCostRevenue - 60m, report.GrossProfit);
        var costed = Assert.Single(report.TopItems);
        Assert.Equal("Costed", costed.Name);
    }

    [Fact]
    public async Task A_voided_cart_with_content_is_listed_with_its_reason_and_approver()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        var item = await CreateItemAsync(client, "Voidable", 40m);
        _ = await client.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(item.Id, null, 1m));
        _ = await client.PostAsJsonAsync("/transactions/cart/void", new VoidCartRequest(TestSessions.AdminPin, "Customer left"));

        var report = await GetAsync<VoidReportDto>(client, "/reports/voids");

        Assert.Equal(1, report.Count);
        Assert.Equal(40m, report.Amount);
        var row = Assert.Single(report.Voids.Rows);
        Assert.Equal("Customer left", row.Reason);
        Assert.Equal(1, row.LineCount);
        Assert.NotNull(row.ApproverName);
    }

    [Fact]
    public async Task A_refund_is_reported_on_its_own_while_sales_mix_stays_gross()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        var sale = await SellAsync(client, await CreateItemAsync(client, "Refundable", 75m));
        var refund = await client.PostAsJsonAsync($"/transactions/{sale.Id}/refund", new RefundTransactionRequest("Wrong order", TestSessions.AdminPin));
        Assert.Equal(HttpStatusCode.OK, refund.StatusCode);

        var refunds = await GetAsync<RefundReportDto>(client, "/reports/refunds");
        var mix = await GetAsync<SalesMixDto>(client, "/reports/sales-mix");

        Assert.Equal(1, refunds.RefundCount);
        Assert.Equal(75m, refunds.RefundAmount);
        Assert.Equal("Wrong order", Assert.Single(refunds.Refunds.Rows).Reason);
        Assert.Equal(75m, mix.GrossSales);
    }

    [Fact]
    public async Task The_discount_report_splits_a_manual_cart_discount_and_lists_its_reason()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        var item = await CreateItemAsync(client, "Discounted", 100m);
        _ = await client.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(item.Id, null, 1m));
        var discount = await client.PutAsJsonAsync(
            "/transactions/cart/discount",
            new SetManualDiscountRequest("Percent", 10m, "Loyal regular", TestSessions.AdminPin));
        Assert.Equal(HttpStatusCode.OK, discount.StatusCode);
        _ = await client.PostAsJsonAsync("/transactions/cart/payments", new RecordPaymentRequest(PaymentMethod.Cash, 90m));

        var report = await GetAsync<DiscountReportDto>(client, "/reports/discounts");
        var manual = await GetAsync<PageDto<ManualDiscountRowDto>>(client, "/reports/discounts/manual");

        Assert.Equal(10m, report.ManualDiscount);
        Assert.Equal(0m, report.SeniorPwdDiscount);
        Assert.Equal(10m, report.TotalDiscounts);
        var row = Assert.Single(manual.Rows);
        Assert.Equal("Loyal regular", row.Reason);
        Assert.Equal(10m, row.CartDiscount);
    }

    [Fact]
    public async Task Insights_return_the_three_executive_series_and_a_cashier_is_refused()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        _ = await SellAsync(admin, await CreateItemAsync(admin, "Insight", 50m, cost: 20m));
        using var cashier = await TestSessions.CashierClientAsync(admin, "678912", "Cal Cashier");

        var insights = await GetAsync<DashboardInsightsDto>(admin, "/reports/dashboard-insights");
        var forbidden = await cashier.GetAsync(Query("/reports/gross-profit"));
        var csvForbidden = await cashier.GetAsync(Query("/reports/voids.csv"));

        Assert.Single(insights.MarginTrend);
        Assert.Single(insights.Leakage);
        Assert.Equal(100m, insights.CoveragePercent);
        Assert.NotEmpty(insights.Categories);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, csvForbidden.StatusCode);
    }

    [Fact]
    public async Task The_admin_can_export_the_void_listing_as_csv()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);

        var response = await client.GetAsync(Query("/reports/voids.csv"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("Voided At,Branch,Staff", await response.Content.ReadAsStringAsync());
    }
}
