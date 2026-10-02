using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Purch.Application.Auth;
using Purch.Application.Catalog;
using Purch.Application.Onboarding;
using Purch.Application.Pos;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class TransactionLookupByReceiptTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Finds_a_completed_sale_by_its_receipt_number()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await AuthenticatedAdminClientAsync(factory);

        var item = (await (await admin.PostAsJsonAsync("/items", new CreateItemRequest("Coffee", null, null, null, 120m, null, PricingType.Unit))).Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;
        var sale = (await (await admin.PostAsJsonAsync(
            "/transactions/checkout",
            new CheckoutRequest(Guid.NewGuid(), [new AddTransactionLineRequest(item.Id, null, 1m)], false, null, null, new RecordPaymentRequest(PaymentMethod.Cash, 120m))))
            .Content.ReadFromJsonAsync<TransactionDto>(JsonOptions))!;

        var matches = await admin.GetFromJsonAsync<List<TransactionDto>>($"/transactions/by-receipt/{sale.ReceiptNumber}", JsonOptions);

        var match = Assert.Single(matches!);
        Assert.Equal(sale.Id, match.Id);
        Assert.Equal(sale.ReceiptNumber, match.ReceiptNumber);
    }

    [Fact]
    public async Task Returns_nothing_for_a_receipt_number_that_was_never_issued()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await AuthenticatedAdminClientAsync(factory);

        var matches = await admin.GetFromJsonAsync<List<TransactionDto>>("/transactions/by-receipt/999999", JsonOptions);

        Assert.Empty(matches!);
    }

    [Fact]
    public async Task Does_not_return_a_voided_cart_that_never_got_a_receipt_number()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await AuthenticatedAdminClientAsync(factory);

        var item = (await (await admin.PostAsJsonAsync("/items", new CreateItemRequest("Tea", null, null, null, 60m, null, PricingType.Unit))).Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;
        _ = await admin.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(item.Id, null, 1m));
        _ = await admin.PostAsync("/transactions/cart/void", null);

        var matches = await admin.GetFromJsonAsync<List<TransactionDto>>("/transactions/by-receipt/1", JsonOptions);

        Assert.Empty(matches!);
    }

    [Fact]
    public async Task A_sale_found_by_receipt_number_can_then_be_refunded()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await AuthenticatedAdminClientAsync(factory);

        var item = (await (await admin.PostAsJsonAsync("/items", new CreateItemRequest("Sandwich", null, null, null, 90m, null, PricingType.Unit))).Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;
        var sale = (await (await admin.PostAsJsonAsync(
            "/transactions/checkout",
            new CheckoutRequest(Guid.NewGuid(), [new AddTransactionLineRequest(item.Id, null, 1m)], false, null, null, new RecordPaymentRequest(PaymentMethod.Cash, 90m))))
            .Content.ReadFromJsonAsync<TransactionDto>(JsonOptions))!;

        var found = (await admin.GetFromJsonAsync<List<TransactionDto>>($"/transactions/by-receipt/{sale.ReceiptNumber}", JsonOptions))!.Single();

        var refundResponse = await admin.PostAsJsonAsync($"/transactions/{found.Id}/refund", new RefundTransactionRequest("Customer changed their mind", "1234"));

        Assert.Equal(HttpStatusCode.OK, refundResponse.StatusCode);
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
