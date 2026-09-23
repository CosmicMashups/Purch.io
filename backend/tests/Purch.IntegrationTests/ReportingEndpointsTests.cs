using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Purch.Application.Auth;
using Purch.Application.Catalog;
using Purch.Application.Inventory;
using Purch.Application.Onboarding;
using Purch.Application.Pos;
using Purch.Application.Promotions;
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
    public async Task A_reading_counts_item_promo_discounts_so_gross_minus_discounts_equals_net()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var item = (await (await client.PostAsJsonAsync("/items", new CreateItemRequest("Rice", null, null, null, 100m, null, PricingType.Unit))).Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;
        _ = await client.PostAsJsonAsync(
            "/promos/item-discounts",
            new CreateItemDiscountPromoRuleRequest("10% off rice", item.Id, PromoDiscountType.Percentage, 10m, null, null));
        _ = await client.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(item.Id, null, 1m));
        _ = await client.PostAsJsonAsync("/transactions/cart/payments", new RecordPaymentRequest(PaymentMethod.Cash, 200m));

        var reading = await (await client.PostAsync("/reports/x-reading", null)).Content.ReadFromJsonAsync<BirReadingDto>(JsonOptions);

        Assert.Equal(90m, reading!.NetSales);
        Assert.Equal(10m, reading.PromoDiscountTotal);
        Assert.Equal(10m, reading.TotalDiscounts);
        Assert.Equal(100m, reading.GrossSales);
        Assert.Equal(reading.NetSales, reading.GrossSales - reading.TotalDiscounts);
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
    public async Task A_sale_that_reaches_the_server_after_a_z_reading_already_passed_its_number_is_picked_up_by_the_next_reading()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var item = await CreateItemAsync(client, "Late Sale Item", 40m);

        // Receipts 1 and 3 reach the server; number 2 (an offline sale) is still on its device.
        await CheckoutWithReceiptNumberAsync(client, item.Id, 1);
        await CheckoutWithReceiptNumberAsync(client, item.Id, 3);

        var firstResponse = await client.PostAsync("/reports/z-reading", null);
        var first = await firstResponse.Content.ReadFromJsonAsync<BirReadingDto>(JsonOptions);
        Assert.Equal(2, first!.TransactionCount);
        Assert.Equal(3, first.EndingReceiptNumber);
        // The gap is disclosed, not hidden.
        Assert.Equal(2L, Assert.Single(first.MissingReceiptNumbers));
        Assert.Empty(first.LateReceiptNumbers);

        // Now number 2 syncs — its number is below the last reading's ending number (3).
        await CheckoutWithReceiptNumberAsync(client, item.Id, 2);

        var secondResponse = await client.PostAsync("/reports/z-reading", null);
        var second = await secondResponse.Content.ReadFromJsonAsync<BirReadingDto>(JsonOptions);

        // Previously a receipt-number range (> 3) skipped it forever; now it is reported, and labelled late.
        Assert.Equal(1, second!.TransactionCount);
        Assert.Equal(40m, second.NetSales);
        Assert.Equal(2, second.BeginningReceiptNumber);
        Assert.Equal(2L, Assert.Single(second.LateReceiptNumbers));
        Assert.Equal(80m, second.OldGrandAccumulatedSales);
        Assert.Equal(120m, second.NewGrandAccumulatedSales);

        // And once reported it is not reported again.
        var thirdResponse = await client.PostAsync("/reports/z-reading", null);
        var third = await thirdResponse.Content.ReadFromJsonAsync<BirReadingDto>(JsonOptions);
        Assert.Equal(0, third!.TransactionCount);
    }

    [Fact]
    public async Task An_x_reading_shows_a_late_sale_without_marking_it_reported()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var item = await CreateItemAsync(client, "X Late Item", 25m);
        await CheckoutWithReceiptNumberAsync(client, item.Id, 2);
        _ = await client.PostAsync("/reports/z-reading", null);
        await CheckoutWithReceiptNumberAsync(client, item.Id, 1);

        var firstX = await (await client.PostAsync("/reports/x-reading", null)).Content.ReadFromJsonAsync<BirReadingDto>(JsonOptions);
        var secondX = await (await client.PostAsync("/reports/x-reading", null)).Content.ReadFromJsonAsync<BirReadingDto>(JsonOptions);

        Assert.Equal(1, firstX!.TransactionCount);
        Assert.Equal(1, secondX!.TransactionCount);
    }

    private static async Task<ItemDto> CreateItemAsync(HttpClient client, string name, decimal price)
    {
        var response = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest(name, null, null, null, price, null, PricingType.Unit));
        return (await response.Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;
    }

    private static async Task CheckoutWithReceiptNumberAsync(HttpClient client, Guid itemId, long receiptNumber)
    {
        var response = await client.PostAsJsonAsync(
            "/transactions/checkout",
            new CheckoutRequest(
                Guid.NewGuid(),
                [new AddTransactionLineRequest(itemId, null, 1m)],
                false,
                null,
                null,
                new RecordPaymentRequest(PaymentMethod.Cash, 1000m),
                ReceiptNumber: receiptNumber));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
    public async Task The_low_stock_export_defuses_item_names_that_would_run_as_spreadsheet_formulas()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("=HYPERLINK(\"http://evil.example\")", null, null, null, 15m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);
        _ = await client.PutAsJsonAsync($"/items/{item!.Id}/low-stock-threshold", new UpdateLowStockThresholdRequest(10m));

        var csv = await (await client.GetAsync("/reports/inventory/low-stock-export.csv")).Content.ReadAsStringAsync();

        Assert.DoesNotContain("\n=HYPERLINK", csv);
        Assert.DoesNotContain("\"=HYPERLINK", csv);
        Assert.Contains("'=HYPERLINK", csv);
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

    [Fact]
    public async Task The_transactions_export_lists_completed_sales_but_not_voided_carts()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        await CompleteACashSaleAsync(client, 150m);

        var voidedItem = await CreateItemAsync(client, "Voided Item", 40m);
        _ = await client.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(voidedItem.Id, null, 1m));
        _ = await client.PostAsync("/transactions/cart/void", null);

        var from = DateTimeOffset.UtcNow.AddDays(-1);
        var to = DateTimeOffset.UtcNow.AddDays(1);
        var response = await client.GetAsync(
            $"/reports/sales/transactions-export.csv?from={Uri.EscapeDataString(from.ToString("o"))}&to={Uri.EscapeDataString(to.ToString("o"))}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType!.MediaType);
        var csv = await response.Content.ReadAsStringAsync();
        var lines = csv.TrimEnd('\n', '\r').Split('\n');
        Assert.Equal(2, lines.Length); // header + the one completed sale
        Assert.Contains("150", lines[1]);
        Assert.DoesNotContain("40", csv);
    }

    [Fact]
    public async Task The_transactions_export_is_refused_to_a_manager()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await AuthenticatedAdminClientAsync(factory);
        _ = await admin.PostAsJsonAsync(
            "/staff",
            new CreateStaffRequest("Mae Manager", Role.Manager, ScopeType.Tenant, null, null, "5678"));
        var devices = await admin.GetFromJsonAsync<List<DeviceDto>>("/devices", JsonOptions);

        using var managerClient = factory.CreateClient();
        var loginResponse = await managerClient.PostAsJsonAsync("/auth/login", new LoginRequest(devices!.Single().PairingCode, "5678"));
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<LoginResponseBody>(JsonOptions);
        managerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginBody!.AccessToken);

        var from = DateTimeOffset.UtcNow.AddDays(-1);
        var to = DateTimeOffset.UtcNow.AddDays(1);
        var response = await managerClient.GetAsync(
            $"/reports/sales/transactions-export.csv?from={Uri.EscapeDataString(from.ToString("o"))}&to={Uri.EscapeDataString(to.ToString("o"))}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
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
