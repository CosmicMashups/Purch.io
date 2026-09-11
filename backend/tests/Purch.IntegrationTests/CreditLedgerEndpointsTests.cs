using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Purch.Application.Auth;
using Purch.Application.Catalog;
using Purch.Application.CreditLedger;
using Purch.Application.Onboarding;
using Purch.Application.Pos;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class CreditLedgerEndpointsTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Creating_a_ledger_is_rejected_when_credit_ledger_is_not_enabled_for_the_tenant()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var response = await client.PostAsJsonAsync(
            "/credit-ledger",
            new CreateCustomerCreditLedgerRequest("Juan Dela Cruz", "09171234567", null, 1000m, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Creating_a_ledger_succeeds_once_credit_ledger_is_enabled()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        await EnableCreditLedgerAsync(client);

        var response = await client.PostAsJsonAsync(
            "/credit-ledger",
            new CreateCustomerCreditLedgerRequest("Juan Dela Cruz", "09171234567", null, 1000m, null));
        var ledger = await response.Content.ReadFromJsonAsync<CustomerCreditLedgerDto>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0m, ledger!.Balance);
        Assert.Equal(1000m, ledger.CreditLimit);
    }

    [Fact]
    public async Task A_utang_sale_within_the_credit_limit_completes_and_increases_the_balance()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        await EnableCreditLedgerAsync(client);

        var ledgerResponse = await client.PostAsJsonAsync(
            "/credit-ledger",
            new CreateCustomerCreditLedgerRequest("Maria Santos", "09181234567", null, 500m, null));
        var ledger = await ledgerResponse.Content.ReadFromJsonAsync<CustomerCreditLedgerDto>(JsonOptions);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Rice (5kg)", null, null, null, 300m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);
        _ = await client.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(item!.Id, null, 1m));

        var paymentResponse = await client.PostAsJsonAsync(
            "/transactions/cart/payments",
            new RecordPaymentRequest(PaymentMethod.UtangCredit, null, ledger!.Id));
        var completed = await paymentResponse.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, paymentResponse.StatusCode);
        Assert.Equal(TransactionStatus.Completed, completed!.Status);

        var ledgersResponse = await client.GetFromJsonAsync<List<CustomerCreditLedgerDto>>("/credit-ledger", JsonOptions);
        var updatedLedger = ledgersResponse!.Single(l => l.Id == ledger.Id);
        Assert.Equal(300m, updatedLedger.Balance);
    }

    [Fact]
    public async Task A_utang_sale_that_would_exceed_the_credit_limit_is_rejected()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        await EnableCreditLedgerAsync(client);

        var ledgerResponse = await client.PostAsJsonAsync(
            "/credit-ledger",
            new CreateCustomerCreditLedgerRequest("Pedro Reyes", "09191234567", null, 100m, null));
        var ledger = await ledgerResponse.Content.ReadFromJsonAsync<CustomerCreditLedgerDto>(JsonOptions);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Cooking Oil", null, null, null, 250m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);
        _ = await client.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(item!.Id, null, 1m));

        var paymentResponse = await client.PostAsJsonAsync(
            "/transactions/cart/payments",
            new RecordPaymentRequest(PaymentMethod.UtangCredit, null, ledger!.Id));

        Assert.Equal(HttpStatusCode.BadRequest, paymentResponse.StatusCode);
    }

    [Fact]
    public async Task Recording_a_repayment_reduces_the_balance_but_not_below_zero()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        await EnableCreditLedgerAsync(client);

        var ledgerResponse = await client.PostAsJsonAsync(
            "/credit-ledger",
            new CreateCustomerCreditLedgerRequest("Ana Cruz", "09201234567", null, 1000m, null));
        var ledger = await ledgerResponse.Content.ReadFromJsonAsync<CustomerCreditLedgerDto>(JsonOptions);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Canned Sardines", null, null, null, 400m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);
        _ = await client.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(item!.Id, null, 1m));
        _ = await client.PostAsJsonAsync(
            "/transactions/cart/payments",
            new RecordPaymentRequest(PaymentMethod.UtangCredit, null, ledger!.Id));

        var overpayResponse = await client.PostAsJsonAsync(
            $"/credit-ledger/{ledger.Id}/payments",
            new RecordCreditPaymentRequest(500m, null));
        Assert.Equal(HttpStatusCode.BadRequest, overpayResponse.StatusCode);

        var paymentResponse = await client.PostAsJsonAsync(
            $"/credit-ledger/{ledger.Id}/payments",
            new RecordCreditPaymentRequest(400m, "Paid in full"));
        var paidLedger = await paymentResponse.Content.ReadFromJsonAsync<CustomerCreditLedgerDto>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, paymentResponse.StatusCode);
        Assert.Equal(0m, paidLedger!.Balance);
    }

    [Fact]
    public async Task Reminders_list_only_ledgers_with_a_balance_due_within_the_lookahead_window()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        await EnableCreditLedgerAsync(client);

        var overdueLedgerResponse = await client.PostAsJsonAsync(
            "/credit-ledger",
            new CreateCustomerCreditLedgerRequest("Overdue Customer", "09211234567", null, 1000m, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3))));
        var overdueLedger = await overdueLedgerResponse.Content.ReadFromJsonAsync<CustomerCreditLedgerDto>(JsonOptions);

        var farFutureLedgerResponse = await client.PostAsJsonAsync(
            "/credit-ledger",
            new CreateCustomerCreditLedgerRequest("Future Customer", "09221234567", null, 1000m, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(60))));
        _ = await farFutureLedgerResponse.Content.ReadFromJsonAsync<CustomerCreditLedgerDto>(JsonOptions);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Bread", null, null, null, 60m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        // Give both a balance so the far-future one would show up if the lookahead filter were broken.
        foreach (var id in new[] { overdueLedger!.Id })
        {
            _ = await client.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(item!.Id, null, 1m));
            _ = await client.PostAsJsonAsync("/transactions/cart/payments", new RecordPaymentRequest(PaymentMethod.UtangCredit, null, id));
        }

        var remindersResponse = await client.GetAsync("/credit-ledger/reminders?withinDays=7");
        var reminders = await remindersResponse.Content.ReadFromJsonAsync<List<CreditReminderDto>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, remindersResponse.StatusCode);
        var reminder = Assert.Single(reminders!);
        Assert.Equal(overdueLedger.Id, reminder.Id);
        Assert.True(reminder.IsOverdue);
    }

    private static async Task EnableCreditLedgerAsync(HttpClient client)
    {
        _ = await client.PutAsJsonAsync("/tenant/settings/credit-ledger", new UpdateCreditLedgerSettingRequest(true));
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
