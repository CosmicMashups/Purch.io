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

    [Fact]
    public async Task Paying_cash_with_enough_tendered_completes_the_sale_and_issues_a_receipt_number()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Soft Drink", null, null, null, 25m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        _ = await client.PostAsJsonAsync(
            "/transactions/cart/lines",
            new AddTransactionLineRequest(item!.Id, null, 2m));

        var paymentResponse = await client.PostAsJsonAsync(
            "/transactions/cart/payments",
            new RecordPaymentRequest(PaymentMethod.Cash, 100m));

        Assert.Equal(HttpStatusCode.OK, paymentResponse.StatusCode);
        var completed = await paymentResponse.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);
        Assert.Equal(TransactionStatus.Completed, completed!.Status);
        Assert.Equal(1, completed.ReceiptNumber);
        var payment = Assert.Single(completed.Payments);
        Assert.Equal(50m, payment.Amount);
        Assert.Equal(50m, payment.ChangeGiven);

        var nextCart = await client.GetFromJsonAsync<TransactionDto>("/transactions/cart", JsonOptions);
        Assert.NotEqual(completed.Id, nextCart!.Id);
    }

    [Fact]
    public async Task Cash_tendered_less_than_the_total_is_rejected()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Energy Drink", null, null, null, 60m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        _ = await client.PostAsJsonAsync(
            "/transactions/cart/lines",
            new AddTransactionLineRequest(item!.Id, null, 1m));

        var paymentResponse = await client.PostAsJsonAsync(
            "/transactions/cart/payments",
            new RecordPaymentRequest(PaymentMethod.Cash, 10m));

        Assert.Equal(HttpStatusCode.BadRequest, paymentResponse.StatusCode);
    }

    [Fact]
    public async Task Paying_with_manual_gcash_qr_completes_the_sale_without_requiring_tendered_amount()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Notebook", null, null, null, 45m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        _ = await client.PostAsJsonAsync(
            "/transactions/cart/lines",
            new AddTransactionLineRequest(item!.Id, null, 1m));

        var paymentResponse = await client.PostAsJsonAsync(
            "/transactions/cart/payments",
            new RecordPaymentRequest(PaymentMethod.ManualGcashQr, null));

        Assert.Equal(HttpStatusCode.OK, paymentResponse.StatusCode);
        var completed = await paymentResponse.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);
        Assert.Equal(TransactionStatus.Completed, completed!.Status);
    }

    [Fact]
    public async Task Paying_via_qr_ph_is_rejected_as_not_yet_available()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Umbrella", null, null, null, 199m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        _ = await client.PostAsJsonAsync(
            "/transactions/cart/lines",
            new AddTransactionLineRequest(item!.Id, null, 1m));

        var paymentResponse = await client.PostAsJsonAsync(
            "/transactions/cart/payments",
            new RecordPaymentRequest(PaymentMethod.QrPh, null));

        Assert.Equal(HttpStatusCode.BadRequest, paymentResponse.StatusCode);
    }

    [Fact]
    public async Task Receipt_numbers_increment_sequentially_per_device_across_separate_sales()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Pen", null, null, null, 10m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        _ = await client.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(item!.Id, null, 1m));
        var firstPayment = await client.PostAsJsonAsync("/transactions/cart/payments", new RecordPaymentRequest(PaymentMethod.Cash, 10m));
        var firstCompleted = await firstPayment.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);

        _ = await client.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(item.Id, null, 1m));
        var secondPayment = await client.PostAsJsonAsync("/transactions/cart/payments", new RecordPaymentRequest(PaymentMethod.Cash, 10m));
        var secondCompleted = await secondPayment.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);

        Assert.Equal(1, firstCompleted!.ReceiptNumber);
        Assert.Equal(2, secondCompleted!.ReceiptNumber);
    }

    [Fact]
    public async Task Applying_the_senior_pwd_discount_takes_twenty_percent_off_the_total()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Rice Meal", null, null, null, 100m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        _ = await client.PostAsJsonAsync(
            "/transactions/cart/lines",
            new AddTransactionLineRequest(item!.Id, null, 1m));

        var discountResponse = await client.PutAsJsonAsync(
            "/transactions/cart/senior-pwd-discount",
            new ApplySeniorPwdDiscountRequest(true));

        Assert.Equal(HttpStatusCode.OK, discountResponse.StatusCode);
        var discounted = await discountResponse.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);
        Assert.True(discounted!.SeniorPwdDiscountApplied);
        Assert.Equal(20m, discounted.DiscountAmount);
        Assert.Equal(80m, discounted.TotalAmount);
    }

    [Fact]
    public async Task The_discount_recalculates_when_a_line_is_added_after_it_is_applied()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Sandwich", null, null, null, 50m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        _ = await client.PostAsJsonAsync(
            "/transactions/cart/lines",
            new AddTransactionLineRequest(item!.Id, null, 1m));
        _ = await client.PutAsJsonAsync(
            "/transactions/cart/senior-pwd-discount",
            new ApplySeniorPwdDiscountRequest(true));

        var secondAddResponse = await client.PostAsJsonAsync(
            "/transactions/cart/lines",
            new AddTransactionLineRequest(item.Id, null, 1m));
        var cart = await secondAddResponse.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);

        Assert.Equal(20m, cart!.DiscountAmount);
        Assert.Equal(80m, cart.TotalAmount);
    }

    [Fact]
    public async Task Turning_the_discount_back_off_restores_the_full_total()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Coffee", null, null, null, 40m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        _ = await client.PostAsJsonAsync(
            "/transactions/cart/lines",
            new AddTransactionLineRequest(item!.Id, null, 1m));
        _ = await client.PutAsJsonAsync(
            "/transactions/cart/senior-pwd-discount",
            new ApplySeniorPwdDiscountRequest(true));

        var offResponse = await client.PutAsJsonAsync(
            "/transactions/cart/senior-pwd-discount",
            new ApplySeniorPwdDiscountRequest(false));
        var cart = await offResponse.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);

        Assert.False(cart!.SeniorPwdDiscountApplied);
        Assert.Equal(0m, cart.DiscountAmount);
        Assert.Equal(40m, cart.TotalAmount);
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
