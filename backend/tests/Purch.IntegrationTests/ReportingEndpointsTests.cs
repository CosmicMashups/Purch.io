using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Purch.Application.Auth;
using Purch.Application.Catalog;
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
