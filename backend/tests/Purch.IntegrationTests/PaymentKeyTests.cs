using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Purch.Application.Catalog;
using Purch.Application.Pos;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class PaymentKeyTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static async Task AddRiceAsync(HttpClient client)
    {
        var item = (await (await client.PostAsJsonAsync("/items", new CreateItemRequest("Rice", null, null, null, 200m, null, PricingType.Unit)))
            .Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;
        _ = await client.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(item.Id, null, 1m));
    }

    [Fact]
    public async Task Repeating_a_payment_key_returns_the_same_sale_instead_of_a_404_or_a_second_payment()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        await AddRiceAsync(admin);
        var request = new RecordPaymentRequest(PaymentMethod.Cash, 200m, PaymentKey: Guid.NewGuid());

        var first = await (await admin.PostAsJsonAsync("/transactions/cart/payments", request)).Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);
        var retry = await admin.PostAsJsonAsync("/transactions/cart/payments", request);

        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        var again = await retry.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);
        Assert.Equal(first!.Id, again!.Id);
        Assert.Equal(first.ReceiptNumber, again.ReceiptNumber);
        Assert.Single(again.Payments);
    }

    [Fact]
    public async Task Cash_tendered_above_the_cart_limit_is_a_400_and_a_missing_cart_names_its_resource()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);

        var none = await admin.PostAsJsonAsync("/transactions/cart/payments", new RecordPaymentRequest(PaymentMethod.Cash, 200m));
        Assert.Equal(HttpStatusCode.NotFound, none.StatusCode);
        Assert.Equal("Open cart", (await none.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("resource").GetString());

        await AddRiceAsync(admin);
        var tooMuch = await admin.PostAsJsonAsync("/transactions/cart/payments", new RecordPaymentRequest(PaymentMethod.Cash, 10_000_000_000m));
        Assert.Equal(HttpStatusCode.BadRequest, tooMuch.StatusCode);
    }

    [Fact]
    public async Task A_stale_expected_total_is_a_409_with_a_typed_code()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        await AddRiceAsync(admin);

        var stale = await admin.PostAsJsonAsync("/transactions/cart/payments", new RecordPaymentRequest(PaymentMethod.Cash, 500m, ExpectedTotal: 150m));

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("total_changed", (await stale.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }
}
