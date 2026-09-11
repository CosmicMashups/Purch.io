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
public sealed class PosEndpointsTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Getting_the_cart_creates_an_empty_open_cart_on_first_request()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var response = await client.GetAsync("/transactions/cart");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cart = await response.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);
        Assert.Equal(TransactionStatus.Open, cart!.Status);
        Assert.Empty(cart.Lines);
        Assert.Equal(0m, cart.TotalAmount);
    }

    [Fact]
    public async Task Getting_the_cart_twice_returns_the_same_open_transaction()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var first = await client.GetFromJsonAsync<TransactionDto>("/transactions/cart", JsonOptions);
        var second = await client.GetFromJsonAsync<TransactionDto>("/transactions/cart", JsonOptions);

        Assert.Equal(first!.Id, second!.Id);
    }

    [Fact]
    public async Task Adding_a_line_computes_the_total_and_adding_the_same_item_again_merges_quantity()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Bottled Water", null, null, null, 15m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        var firstAdd = await client.PostAsJsonAsync(
            "/transactions/cart/lines",
            new AddTransactionLineRequest(item!.Id, null, 2m));
        Assert.Equal(HttpStatusCode.OK, firstAdd.StatusCode);

        var secondAdd = await client.PostAsJsonAsync(
            "/transactions/cart/lines",
            new AddTransactionLineRequest(item.Id, null, 3m));
        var cart = await secondAdd.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);

        var line = Assert.Single(cart!.Lines);
        Assert.Equal(5m, line.Quantity);
        Assert.Equal(75m, line.LineTotal);
        Assert.Equal(75m, cart.TotalAmount);
    }

    [Fact]
    public async Task Updating_a_line_quantity_recomputes_the_total()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Instant Coffee", null, null, null, 8m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        var addResponse = await client.PostAsJsonAsync(
            "/transactions/cart/lines",
            new AddTransactionLineRequest(item!.Id, null, 1m));
        var cartAfterAdd = await addResponse.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);
        var lineId = cartAfterAdd!.Lines.Single().Id;

        var updateResponse = await client.PutAsJsonAsync(
            $"/transactions/cart/lines/{lineId}",
            new UpdateTransactionLineRequest(4m));
        var updated = await updateResponse.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);

        Assert.Equal(32m, updated!.TotalAmount);
    }

    [Fact]
    public async Task Removing_a_line_recomputes_the_total()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Candy Bar", null, null, null, 20m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        var addResponse = await client.PostAsJsonAsync(
            "/transactions/cart/lines",
            new AddTransactionLineRequest(item!.Id, null, 1m));
        var cartAfterAdd = await addResponse.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);
        var lineId = cartAfterAdd!.Lines.Single().Id;

        var removeResponse = await client.DeleteAsync($"/transactions/cart/lines/{lineId}");
        var afterRemove = await removeResponse.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);

        Assert.Empty(afterRemove!.Lines);
        Assert.Equal(0m, afterRemove.TotalAmount);
    }

    [Fact]
    public async Task Adding_a_negative_quantity_is_rejected()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Chips", null, null, null, 25m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        var response = await client.PostAsJsonAsync(
            "/transactions/cart/lines",
            new AddTransactionLineRequest(item!.Id, null, -1m));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Voiding_the_cart_then_getting_the_cart_starts_a_fresh_one()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var original = await client.GetFromJsonAsync<TransactionDto>("/transactions/cart", JsonOptions);

        var voidResponse = await client.PostAsync("/transactions/cart/void", null);
        var voided = await voidResponse.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);
        Assert.Equal(TransactionStatus.Voided, voided!.Status);

        var fresh = await client.GetFromJsonAsync<TransactionDto>("/transactions/cart", JsonOptions);
        Assert.NotEqual(original!.Id, fresh!.Id);
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
