using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Purch.Application.Auth;
using Purch.Application.Catalog;
using Purch.Application.Inventory;
using Purch.Application.Onboarding;
using Purch.Application.Pos;
using Purch.Application.Reporting;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class ReportingEndpointsTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task An_x_reading_with_no_sales_yet_is_empty_but_succeeds()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var response = await client.PostAsync("/reports/x-reading", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var reading = await response.Content.ReadFromJsonAsync<BirReadingDto>(JsonOptions);
        Assert.Equal(BirReadingType.X, reading!.Type);
        Assert.Equal(0, reading.TransactionCount);
        Assert.Null(reading.BeginningReceiptNumber);
        Assert.Null(reading.EndingReceiptNumber);
        Assert.Equal(0m, reading.NetSales);
    }

    [Fact]
    public async Task An_x_reading_summarizes_completed_sales_without_advancing_counters()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        await CompleteACashSaleAsync(client, 100m);
        await CompleteACashSaleAsync(client, 50m);

        var firstReading = await client.PostAsync("/reports/x-reading", null);
        var first = await firstReading.Content.ReadFromJsonAsync<BirReadingDto>(JsonOptions);

        Assert.Equal(2, first!.TransactionCount);
        Assert.Equal(1, first.BeginningReceiptNumber);
        Assert.Equal(2, first.EndingReceiptNumber);
        Assert.Equal(150m, first.NetSales);
        Assert.Equal(0, first.ResetCounter);

        var secondReading = await client.PostAsync("/reports/x-reading", null);
        var second = await secondReading.Content.ReadFromJsonAsync<BirReadingDto>(JsonOptions);

        Assert.Equal(2, second!.TransactionCount);
        Assert.Equal(0, second.ResetCounter);
    }

    [Fact]
    public async Task A_z_reading_advances_the_reset_counter_and_grand_accumulated_sales()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        await CompleteACashSaleAsync(client, 100m);

        var response = await client.PostAsync("/reports/z-reading", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var reading = await response.Content.ReadFromJsonAsync<BirReadingDto>(JsonOptions);
        Assert.Equal(BirReadingType.Z, reading!.Type);
        Assert.Equal(100m, reading.NetSales);
        Assert.Equal(0m, reading.OldGrandAccumulatedSales);
        Assert.Equal(100m, reading.NewGrandAccumulatedSales);
        Assert.Equal(1, reading.ResetCounter);
    }

    [Fact]
    public async Task A_second_z_reading_only_covers_sales_since_the_first_one()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        await CompleteACashSaleAsync(client, 100m);
        _ = await client.PostAsync("/reports/z-reading", null);

        await CompleteACashSaleAsync(client, 30m);
        var response = await client.PostAsync("/reports/z-reading", null);
        var reading = await response.Content.ReadFromJsonAsync<BirReadingDto>(JsonOptions);

        Assert.Equal(1, reading!.TransactionCount);
        Assert.Equal(30m, reading.NetSales);
        Assert.Equal(100m, reading.OldGrandAccumulatedSales);
        Assert.Equal(130m, reading.NewGrandAccumulatedSales);
        Assert.Equal(2, reading.ResetCounter);
    }

    [Fact]
    public async Task A_reading_includes_voided_carts_since_the_last_z_reading()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Chips", null, null, null, 25m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);
        _ = await client.PostAsJsonAsync(
            "/transactions/cart/lines",
            new AddTransactionLineRequest(item!.Id, null, 1m));
        _ = await client.PostAsync("/transactions/cart/void", null);

        var response = await client.PostAsync("/reports/x-reading", null);
        var reading = await response.Content.ReadFromJsonAsync<BirReadingDto>(JsonOptions);

        Assert.Equal(1, reading!.VoidedCount);
        Assert.Equal(25m, reading.VoidedAmount);
    }

    [Fact]
    public async Task The_sales_dashboard_reflects_a_completed_sale_in_today_and_the_trend()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        await CompleteACashSaleAsync(client, 150m);

        var response = await client.GetAsync("/reports/sales-dashboard");
        var dashboard = await response.Content.ReadFromJsonAsync<SalesDashboardDto>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(150m, dashboard!.RevenueToday);
        Assert.Equal(150m, dashboard.RevenueLast7Days);
        Assert.Equal(150m, dashboard.RevenueLast30Days);
        Assert.Equal(14, dashboard.Trend.Count);
        Assert.Equal(150m, dashboard.Trend[^1].Revenue);
        var topItem = Assert.Single(dashboard.TopSellingItems);
        Assert.Equal(150m, topItem.Revenue);
        var branchRow = Assert.Single(dashboard.BranchComparison);
        Assert.Equal(150m, branchRow.Revenue);
    }

    [Fact]
    public async Task The_movement_summary_groups_recorded_movements_by_type_within_the_date_range()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Canned Goods", null, null, null, 30m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);
        var branches = await client.GetFromJsonAsync<List<BranchDto>>("/branches", JsonOptions);
        var branchId = branches!.Single().Id;

        _ = await client.PostAsJsonAsync(
            "/inventory/movements",
            new RecordMovementRequest(item!.Id, branchId, MovementType.StockIn, 20m, null, null, null, null));

        var fromUtc = DateTimeOffset.UtcNow.AddDays(-1);
        var toUtc = DateTimeOffset.UtcNow.AddDays(1);
        var response = await client.GetAsync(
            $"/reports/inventory/movement-summary?from={Uri.EscapeDataString(fromUtc.ToString("O"))}&to={Uri.EscapeDataString(toUtc.ToString("O"))}");
        var summary = await response.Content.ReadFromJsonAsync<MovementSummaryDto>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stockInRow = summary!.ByType.Single(row => row.Type == MovementType.StockIn);
        Assert.Equal(20m, stockInRow.TotalQuantity);
        Assert.Equal(1, stockInRow.MovementCount);
    }

    [Fact]
    public async Task The_low_stock_export_returns_a_csv_with_items_under_their_threshold()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Instant Noodles", null, null, null, 15m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);
        _ = await client.PutAsJsonAsync(
            $"/items/{item!.Id}/low-stock-threshold",
            new UpdateLowStockThresholdRequest(10m));

        var response = await client.GetAsync("/reports/inventory/low-stock-export.csv");
        var csv = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType!.MediaType);
        Assert.Contains("Instant Noodles", csv);
    }

    [Fact]
    public async Task The_staff_performance_report_summarizes_sales_and_shift_attendance()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        await CompleteACashSaleAsync(client, 75m);

        var from = DateTimeOffset.UtcNow.AddDays(-1);
        var to = DateTimeOffset.UtcNow.AddDays(1);
        var response = await client.GetAsync(
            $"/reports/staff-performance?from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}");
        var report = await response.Content.ReadFromJsonAsync<StaffPerformanceReportDto>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var salesRow = Assert.Single(report!.Sales);
        Assert.Equal(75m, salesRow.TotalSales);
        Assert.Equal(1, salesRow.TransactionCount);
    }

    [Fact]
    public async Task The_department_sales_report_splits_revenue_between_a_departments_items_and_general_items()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var branches = await client.GetFromJsonAsync<List<BranchDto>>("/branches", JsonOptions);
        var branchId = branches!.Single().Id;
        var departmentResponse = await client.PostAsJsonAsync(
            $"/branches/{branchId}/departments",
            new CreateDepartmentRequest("Concessionaire Stall", null));
        var department = await departmentResponse.Content.ReadFromJsonAsync<DepartmentDto>(JsonOptions);

        var departmentItemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Fishball", null, null, null, 20m, null, PricingType.Unit));
        var departmentItem = await departmentItemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);
        _ = await client.PutAsJsonAsync(
            $"/items/{departmentItem!.Id}/department",
            new UpdateItemDepartmentRequest(department!.Id));

        var generalItemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Bottled Water", null, null, null, 15m, null, PricingType.Unit));
        var generalItem = await generalItemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        _ = await client.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(departmentItem.Id, null, 1m));
        _ = await client.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(generalItem!.Id, null, 1m));
        _ = await client.PostAsJsonAsync("/transactions/cart/payments", new RecordPaymentRequest(PaymentMethod.Cash, 35m));

        var fromUtc = DateTimeOffset.UtcNow.AddDays(-1);
        var toUtc = DateTimeOffset.UtcNow.AddDays(1);
        var response = await client.GetAsync(
            $"/reports/department-sales?from={Uri.EscapeDataString(fromUtc.ToString("O"))}&to={Uri.EscapeDataString(toUtc.ToString("O"))}");
        var report = await response.Content.ReadFromJsonAsync<List<DepartmentSalesSummaryDto>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, report!.Count);
        var departmentRow = report.Single(row => row.DepartmentId == department.Id);
        Assert.Equal(20m, departmentRow.Revenue);
        var generalRow = report.Single(row => row.DepartmentId == null);
        Assert.Equal(15m, generalRow.Revenue);
    }

    private static async Task CompleteACashSaleAsync(HttpClient client, decimal price)
    {
        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest($"Item-{Guid.NewGuid():N}", null, null, null, price, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);
        _ = await client.PostAsJsonAsync(
            "/transactions/cart/lines",
            new AddTransactionLineRequest(item!.Id, null, 1m));
        _ = await client.PostAsJsonAsync(
            "/transactions/cart/payments",
            new RecordPaymentRequest(PaymentMethod.Cash, price));
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
