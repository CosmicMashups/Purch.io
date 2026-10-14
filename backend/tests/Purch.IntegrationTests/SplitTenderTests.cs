using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
using Purch.Application.Catalog;
using Purch.Application.CreditLedger;
using Purch.Application.Onboarding;
using Purch.Application.Pos;
using Purch.Application.Reporting;
using Purch.Application.Shifts;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class SplitTenderTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static PaymentPartRequest Part(PaymentMethod method, decimal amount, decimal? tendered = null, Guid? ledger = null, string? reference = null)
        => new(method, amount, tendered, ledger, reference);

    private static RecordPaymentRequest Split(params PaymentPartRequest[] parts)
        => new(parts[0].Method, null, Parts: parts);

    private static async Task<TransactionDto> ReadAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions))!;
    }

    private static async Task<ItemDto> CreateItemAsync(HttpClient client, decimal price)
    {
        var response = await client.PostAsJsonAsync("/items", new CreateItemRequest("Split Rice", null, null, null, price, null, PricingType.Unit));
        Assert.True(response.IsSuccessStatusCode);
        return (await response.Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;
    }

    private static async Task<CustomerCreditLedgerDto> CreateLedgerAsync(HttpClient admin, decimal limit)
    {
        _ = await admin.PutAsJsonAsync("/tenant/settings/credit-ledger", new UpdateCreditLedgerSettingRequest(true));
        var response = await admin.PostAsJsonAsync("/credit-ledger", new CreateCustomerCreditLedgerRequest("Split Customer", "09171112222", null, limit, null));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CustomerCreditLedgerDto>(JsonOptions))!;
    }

    private static async Task<decimal> BalanceAsync(HttpClient admin, Guid ledgerId)
        => (await admin.GetFromJsonAsync<List<CustomerCreditLedgerDto>>("/credit-ledger", JsonOptions))!.Single(l => l.Id == ledgerId).Balance;

    private async Task<T> ScalarAsync<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            _ = command.Parameters.AddWithValue(name, value);
        }

        return (T)(await command.ExecuteScalarAsync())!;
    }

    private Task<long> PaymentRowsAsync(Guid transactionId)
        => ScalarAsync<long>("SELECT COUNT(*) FROM \"Payments\" WHERE \"TransactionId\" = @id", ("id", transactionId));

    [Fact]
    public async Task A_split_payment_writes_one_row_per_part_in_sequence_under_one_receipt_number()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        using var cashier = await TestSessions.CashierClientAsync(admin);
        var item = await CreateItemAsync(admin, 150m);
        _ = await cashier.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(item.Id, null, 1m));

        var sale = await ReadAsync(await cashier.PostAsJsonAsync("/transactions/cart/payments",
            Split(Part(PaymentMethod.Cash, 100m, 200m), Part(PaymentMethod.ManualGcashQr, 50m, reference: "GC-778"))));

        Assert.Equal(TransactionStatus.Completed, sale.Status);
        Assert.NotNull(sale.ReceiptNumber);
        Assert.Equal(2, sale.Payments.Count);
        Assert.Equal([0, 1], sale.Payments.Select(p => p.Sequence));
        Assert.Equal(PaymentMethod.Cash, sale.Payments[0].Method);
        Assert.Equal(100m, sale.Payments[0].Amount);
        Assert.Equal(100m, sale.Payments[0].ChangeGiven);
        Assert.Equal("GC-778", sale.Payments[1].Reference);
        Assert.Equal(2L, await PaymentRowsAsync(sale.Id));
        Assert.Equal(1L, await ScalarAsync<long>("SELECT COUNT(DISTINCT \"ReceiptNumber\") FROM \"Transactions\" WHERE \"Id\" = @id", ("id", sale.Id)));
        Assert.Equal(150m, await ScalarAsync<decimal>("SELECT SUM(\"Amount\") FROM \"Payments\" WHERE \"TransactionId\" = @id", ("id", sale.Id)));
    }

    [Fact]
    public async Task A_retried_split_checkout_with_the_same_sale_id_returns_the_same_sale_without_duplicate_payments()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        var item = await CreateItemAsync(admin, 150m);
        var request = new CheckoutRequest(Guid.NewGuid(), [new AddTransactionLineRequest(item.Id, null, 1m)], false, null, null,
            Split(Part(PaymentMethod.Cash, 100m, 100m), Part(PaymentMethod.BankTransfer, 50m)));

        var first = await ReadAsync(await admin.PostAsJsonAsync("/transactions/checkout", request));
        var second = await ReadAsync(await admin.PostAsJsonAsync("/transactions/checkout", request));

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.ReceiptNumber, second.ReceiptNumber);
        Assert.Equal(2, second.Payments.Count);
        Assert.Equal(2L, await PaymentRowsAsync(first.Id));
    }

    [Fact]
    public async Task An_utang_part_respects_the_credit_limit_for_its_own_amount_and_charges_only_that()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        using var cashier = await TestSessions.CashierClientAsync(admin);
        var ledger = await CreateLedgerAsync(admin, 100m);
        var item = await CreateItemAsync(admin, 300m);
        _ = await cashier.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(item.Id, null, 1m));

        var overLimit = await cashier.PostAsJsonAsync("/transactions/cart/payments",
            Split(Part(PaymentMethod.Cash, 150m, 150m), Part(PaymentMethod.UtangCredit, 150m, ledger: ledger.Id)));
        Assert.Equal(HttpStatusCode.BadRequest, overLimit.StatusCode);
        Assert.Equal(0m, await BalanceAsync(admin, ledger.Id));

        var sale = await ReadAsync(await cashier.PostAsJsonAsync("/transactions/cart/payments",
            Split(Part(PaymentMethod.Cash, 200m, 200m), Part(PaymentMethod.UtangCredit, 100m, ledger: ledger.Id))));

        Assert.Equal(TransactionStatus.Completed, sale.Status);
        Assert.Equal(100m, await BalanceAsync(admin, ledger.Id));
        Assert.Equal(100m, await ScalarAsync<decimal>("SELECT SUM(\"Amount\") FROM \"CreditTransactions\" WHERE \"TransactionId\" = @id", ("id", sale.Id)));
    }

    [Fact]
    public async Task Refunding_a_split_sale_with_utang_reverses_the_utang_part_exactly_once_and_lists_the_parts()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        var ledger = await CreateLedgerAsync(admin, 1000m);
        var item = await CreateItemAsync(admin, 300m);
        _ = await admin.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(item.Id, null, 1m));
        var sale = await ReadAsync(await admin.PostAsJsonAsync("/transactions/cart/payments",
            Split(Part(PaymentMethod.Cash, 120m, 200m), Part(PaymentMethod.ManualGcashQr, 80m), Part(PaymentMethod.UtangCredit, 100m, ledger: ledger.Id))));
        Assert.Equal(100m, await BalanceAsync(admin, ledger.Id));

        var refunded = await ReadAsync(await admin.PostAsJsonAsync($"/transactions/{sale.Id}/refund", new RefundTransactionRequest("Customer changed mind", TestSessions.AdminPin)));
        var again = await admin.PostAsJsonAsync($"/transactions/{sale.Id}/refund", new RefundTransactionRequest("Again", TestSessions.AdminPin));

        Assert.Equal(TransactionStatus.Refunded, refunded.Status);
        Assert.Equal([PaymentMethod.Cash, PaymentMethod.ManualGcashQr, PaymentMethod.UtangCredit], refunded.Payments.Select(p => p.Method));
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Equal(0m, await BalanceAsync(admin, ledger.Id));
        Assert.Equal(1L, await ScalarAsync<long>("SELECT COUNT(*) FROM \"CreditTransactions\" WHERE \"TransactionId\" = @id AND \"Amount\" < 0", ("id", sale.Id)));
        Assert.Equal(-100m, await ScalarAsync<decimal>("SELECT SUM(\"Amount\") FROM \"CreditTransactions\" WHERE \"TransactionId\" = @id AND \"Amount\" < 0", ("id", sale.Id)));
        var audit = await ScalarAsync<string>("SELECT \"AfterStateJson\" FROM \"AuditLogs\" WHERE \"TargetEntityId\" = @id AND \"ActionType\" = @t", ("id", sale.Id), ("t", (int)AuditActionType.Refund));
        Assert.Contains("ManualGcashQr", audit);
        Assert.Contains("UtangCredit", audit);
    }

    [Theory]
    [InlineData(99.99, 50, null)]
    [InlineData(100.01, 50, null)]
    [InlineData(100, 50, 60.0)]
    [InlineData(150, 0, null)]
    [InlineData(160, -10, null)]
    public async Task An_invalid_split_is_a_400_and_persists_no_payment_rows(double cash, double gcash, double? gcashTendered)
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        var item = await CreateItemAsync(admin, 150m);
        _ = await admin.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(item.Id, null, 1m));

        var response = await admin.PostAsJsonAsync("/transactions/cart/payments",
            Split(Part(PaymentMethod.Cash, (decimal)cash, (decimal)cash), Part(PaymentMethod.ManualGcashQr, (decimal)gcash, (decimal?)gcashTendered)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var cart = (await admin.GetFromJsonAsync<TransactionDto>("/transactions/cart", JsonOptions))!;
        Assert.Equal(TransactionStatus.Open, cart.Status);
        Assert.Equal(0L, await PaymentRowsAsync(cart.Id));
        Assert.Null(cart.ReceiptNumber);
    }

    [Fact]
    public async Task An_invalid_split_checkout_is_a_400_and_persists_no_payment_rows()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        var item = await CreateItemAsync(admin, 150m);
        var saleId = Guid.NewGuid();

        var response = await admin.PostAsJsonAsync("/transactions/checkout", new CheckoutRequest(saleId, [new AddTransactionLineRequest(item.Id, null, 1m)], false, null, null,
            Split(Part(PaymentMethod.Cash, 100m, 100m), Part(PaymentMethod.ManualGcashQr, 49.99m))));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0L, await ScalarAsync<long>(
            "SELECT COUNT(*) FROM \"Payments\" p JOIN \"Transactions\" t ON t.\"Id\" = p.\"TransactionId\" WHERE t.\"TenantId\" = (SELECT \"TenantId\" FROM \"Items\" WHERE \"Id\" = @id)",
            ("id", item.Id)));
    }

    [Fact]
    public async Task Shift_expected_cash_and_z_reading_count_only_the_cash_part_of_a_split_sale()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        var item = await CreateItemAsync(admin, 150m);
        _ = await admin.PostAsJsonAsync("/shifts/open", new OpenShiftRequest(1000m));
        _ = await admin.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(item.Id, null, 1m));
        _ = await ReadAsync(await admin.PostAsJsonAsync("/transactions/cart/payments",
            Split(Part(PaymentMethod.Cash, 100m, 500m), Part(PaymentMethod.ManualGcashQr, 50m))));

        var z = (await (await admin.PostAsync("/reports/z-reading", null)).Content.ReadFromJsonAsync<BirReadingDto>(JsonOptions))!;
        var closed = (await (await admin.PostAsJsonAsync("/shifts/close", new CloseShiftRequest(1100m, null, null))).Content.ReadFromJsonAsync<ShiftDto>(JsonOptions))!;

        Assert.Equal(1, z.TransactionCount);
        Assert.Equal(150m, z.NetSales);
        Assert.Equal(1100m, closed.ExpectedCashAmount);
        Assert.Equal(0m, closed.VarianceAmount);
    }

    [Fact]
    public async Task A_single_part_list_behaves_like_the_legacy_single_payment()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        var item = await CreateItemAsync(admin, 150m);
        _ = await admin.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(item.Id, null, 1m));

        var sale = await ReadAsync(await admin.PostAsJsonAsync("/transactions/cart/payments", Split(Part(PaymentMethod.Cash, 150m, 200m))));

        var payment = Assert.Single(sale.Payments);
        Assert.Equal(150m, payment.Amount);
        Assert.Equal(50m, payment.ChangeGiven);
    }
}
