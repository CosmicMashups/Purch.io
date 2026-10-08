using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Purch.Application.Catalog;
using Purch.Application.CreditLedger;
using Purch.Application.Inventory;
using Purch.Application.Onboarding;
using Purch.Application.Pos;
using Purch.Application.Shifts;
using Purch.Common.TestUtilities;
using Purch.Domain.Enums;
using Purch.Infrastructure.Persistence;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests.Security;

/// <summary>Shift cash and the rules for returns: an utang repayment is cash in a counted drawer, a refund reverses the sale
/// (stock, utang, drawer), a returned item is worth what was paid, and returns stay in their branch and within 30 days.</summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class ReturnAndShiftRulesTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string AdminPin = "123412";

    private PurchDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<PurchDbContext>().UseNpgsql(postgres.ConnectionString).Options;
        return new PurchDbContext(options, new TestCurrentTenantProvider());
    }

    private static async Task<ItemDto> NewItemAsync(HttpClient client, string name, decimal price)
        => (await (await client.PostAsJsonAsync("/items", new CreateItemRequest(name, null, null, null, price, null, PricingType.Unit))).Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;

    private static async Task<TransactionDto> SellAsync(HttpClient client, ItemDto item, decimal quantity, RecordPaymentRequest payment, bool senior = false)
    {
        var response = await client.PostAsJsonAsync(
            "/transactions/checkout",
            new CheckoutRequest(Guid.NewGuid(), [new AddTransactionLineRequest(item.Id, null, quantity)], senior, null, null, payment));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions))!;
    }

    private static async Task<ShiftDto> CloseExactlyAsync(HttpClient client, decimal count)
    {
        var response = await client.PostAsJsonAsync("/shifts/close", new CloseShiftRequest(count, null, null));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ShiftDto>(JsonOptions))!;
    }

    private static async Task<decimal> StockAsync(HttpClient client, Guid itemId)
        => (await client.GetFromJsonAsync<List<ItemDto>>("/items", JsonOptions))!.Single(i => i.Id == itemId).StockOnHand;

    private static async Task<CustomerCreditLedgerDto> NewLedgerAsync(HttpClient client)
    {
        _ = await client.PutAsJsonAsync("/tenant/settings/credit-ledger", new UpdateCreditLedgerSettingRequest(true));
        return (await (await client.PostAsJsonAsync("/credit-ledger", new CreateCustomerCreditLedgerRequest("Lita Gomez", "09231234567", null, 5000m, null))).Content.ReadFromJsonAsync<CustomerCreditLedgerDto>(JsonOptions))!;
    }

    [Fact]
    public async Task A_repayment_needs_a_shift_open_on_the_register()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        var ledger = await NewLedgerAsync(client);
        _ = await SellAsync(client, await NewItemAsync(client, "Soap", 400m), 1m, new RecordPaymentRequest(PaymentMethod.UtangCredit, null, ledger.Id));

        var noShift = await client.PostAsJsonAsync($"/credit-ledger/{ledger.Id}/payments", new RecordCreditPaymentRequest(100m, null));

        Assert.Equal(HttpStatusCode.BadRequest, noShift.StatusCode);
        Assert.Contains("shift", await noShift.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_repayment_counts_as_cash_in_the_shift_that_took_it()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        var ledger = await NewLedgerAsync(client);
        _ = await SellAsync(client, await NewItemAsync(client, "Soap", 400m), 1m, new RecordPaymentRequest(PaymentMethod.UtangCredit, null, ledger.Id));
        _ = await client.PostAsJsonAsync("/shifts/open", new OpenShiftRequest(1000m));

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/credit-ledger/{ledger.Id}/payments", new RecordCreditPaymentRequest(150m, "Partial"))).StatusCode);

        // 1000 float + 150 repaid: counting only 1000 is a shortage of 150 that needs a manager's approval.
        var short1000 = await client.PostAsJsonAsync("/shifts/close", new CloseShiftRequest(1000m, null, null));
        Assert.Equal(HttpStatusCode.BadRequest, short1000.StatusCode);
        var closed = await CloseExactlyAsync(client, 1150m);
        Assert.Equal(1150m, closed.ExpectedCashAmount);
        Assert.Equal(0m, closed.VarianceAmount);
    }

    [Fact]
    public async Task Refunding_a_cash_sale_takes_the_cash_out_of_the_drawer_and_puts_the_stock_back()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        var shop = TestSessions.ShopOf(client);
        var item = await NewItemAsync(client, "Ramen", 100m);
        _ = await client.PostAsJsonAsync("/inventory/movements", new RecordMovementRequest(item.Id, shop.Tenant.BranchId, MovementType.StockIn, 10m, null, null, null, null));
        _ = await client.PostAsJsonAsync("/shifts/open", new OpenShiftRequest(1000m));
        var sale = await SellAsync(client, item, 2m, new RecordPaymentRequest(PaymentMethod.Cash, 500m));
        Assert.Equal(8m, await StockAsync(client, item.Id));

        var refund = await client.PostAsJsonAsync($"/transactions/{sale.Id}/refund", new RefundTransactionRequest("Customer returned it", AdminPin));

        Assert.Equal(HttpStatusCode.OK, refund.StatusCode);
        Assert.Equal(10m, await StockAsync(client, item.Id));
        // Sold 200 in cash, then paid 200 back: the drawer holds the float again.
        var closed = await CloseExactlyAsync(client, 1000m);
        Assert.Equal(1000m, closed.ExpectedCashAmount);
    }

    [Fact]
    public async Task Refunding_a_sale_charged_to_utang_takes_it_off_the_customers_balance()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        var ledger = await NewLedgerAsync(client);
        var sale = await SellAsync(client, await NewItemAsync(client, "Rice", 300m), 1m, new RecordPaymentRequest(PaymentMethod.UtangCredit, null, ledger.Id));
        Assert.Equal(300m, (await client.GetFromJsonAsync<List<CustomerCreditLedgerDto>>("/credit-ledger", JsonOptions))!.Single().Balance);

        var refund = await client.PostAsJsonAsync($"/transactions/{sale.Id}/refund", new RefundTransactionRequest("Wrong item", AdminPin));

        Assert.Equal(HttpStatusCode.OK, refund.StatusCode);
        Assert.Equal(0m, (await client.GetFromJsonAsync<List<CustomerCreditLedgerDto>>("/credit-ledger", JsonOptions))!.Single().Balance);
    }

    [Fact]
    public async Task A_returned_item_is_worth_what_the_customer_actually_paid()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        var item = await NewItemAsync(client, "Medicine", 112m);
        var pricier = await NewItemAsync(client, "Vitamins", 500m);
        // A Senior/PWD sale: VAT comes off, then 20%, so a 112.00 list price is paid as 80.00.
        var sale = await SellAsync(client, item, 1m, new RecordPaymentRequest(PaymentMethod.Cash, 200m), senior: true);
        Assert.Equal(80m, sale.TotalAmount);
        var line = sale.Lines.Single();

        var exchange = await client.PostAsJsonAsync(
            $"/transactions/{sale.Id}/exchange",
            new CreateExchangeRequest([new ReturnLineRequest(line.Id, 1m)], [new ReplacementLineRequest(pricier.Id, null, 1m)], "Upgrade", AdminPin, PaymentMethod.Cash, 1000m));

        Assert.Equal(HttpStatusCode.OK, exchange.StatusCode);
        var adjustment = (await exchange.Content.ReadFromJsonAsync<AdjustmentDto>(JsonOptions))!;
        Assert.Equal(80m, adjustment.ReturnedTotal);
        Assert.Equal(420m, adjustment.PriceDifference);
    }

    [Fact]
    public async Task A_sale_older_than_thirty_days_can_only_be_returned_by_an_owner_level_admin()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        using var cashier = await TestSessions.CashierClientAsync(admin);
        var sale = await SellAsync(cashier, await NewItemAsync(admin, "Ramen", 100m), 1m, new RecordPaymentRequest(PaymentMethod.Cash, 100m));
        await using (var db = NewContext())
        {
            var row = await db.Transactions.IgnoreQueryFilters().SingleAsync(t => t.Id == sale.Id);
            row.CompletedAt = DateTimeOffset.UtcNow.AddDays(-(ReturnPolicy.WindowDays + 5));
            _ = await db.SaveChangesAsync();
        }

        var byCashier = await cashier.PostAsJsonAsync($"/transactions/{sale.Id}/refund", new RefundTransactionRequest("Old", AdminPin));
        Assert.Equal(HttpStatusCode.BadRequest, byCashier.StatusCode);
        Assert.Contains("30 days", await byCashier.Content.ReadAsStringAsync());

        var byAdmin = await admin.PostAsJsonAsync($"/transactions/{sale.Id}/refund", new RefundTransactionRequest("Old", AdminPin));
        Assert.Equal(HttpStatusCode.OK, byAdmin.StatusCode);
    }

    [Fact]
    public async Task A_register_cannot_return_a_sale_made_at_another_branch()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        using var cashier = await TestSessions.CashierClientAsync(admin);
        var otherBranch = (await (await admin.PostAsJsonAsync("/branches", new CreateBranchRequest("Other branch", null))).Content.ReadFromJsonAsync<BranchDto>(JsonOptions))!;
        var sale = await SellAsync(cashier, await NewItemAsync(admin, "Ramen", 100m), 1m, new RecordPaymentRequest(PaymentMethod.Cash, 100m));
        await using (var db = NewContext())
        {
            var row = await db.Transactions.IgnoreQueryFilters().SingleAsync(t => t.Id == sale.Id);
            row.BranchId = otherBranch.Id;
            _ = await db.SaveChangesAsync();
        }

        var byCashier = await cashier.PostAsJsonAsync($"/transactions/{sale.Id}/refund", new RefundTransactionRequest("Wrong branch", AdminPin));
        Assert.Equal(HttpStatusCode.BadRequest, byCashier.StatusCode);
        Assert.Contains("another branch", await byCashier.Content.ReadAsStringAsync());

        var byAdmin = await admin.PostAsJsonAsync($"/transactions/{sale.Id}/refund", new RefundTransactionRequest("Owner override", AdminPin));
        Assert.Equal(HttpStatusCode.OK, byAdmin.StatusCode);
    }

    [Fact]
    public void Paid_values_of_all_lines_add_up_to_the_sale_total()
    {
        var sale = new Purch.Domain.Entities.Transaction { TotalAmount = 150m };
        var a = new Purch.Domain.Entities.TransactionLine { Quantity = 2, LineTotal = 100m, PromoDiscountAmount = 0m };
        var b = new Purch.Domain.Entities.TransactionLine { Quantity = 1, LineTotal = 100m, PromoDiscountAmount = 50m };
        List<Purch.Domain.Entities.TransactionLine> lines = [a, b];

        // b's own promotion takes 50 off, leaving 100 + 50; the sale total of 150 is exactly that, so nothing more is shared.
        Assert.Equal(100m, ReturnPolicy.PaidValue(sale, lines, a, 2m));
        Assert.Equal(50m, ReturnPolicy.PaidValue(sale, lines, b, 1m));
        Assert.Equal(50m, ReturnPolicy.PaidValue(sale, lines, a, 1m));
        Assert.Equal(150m, ReturnPolicy.PaidValue(sale, lines, a, 2m) + ReturnPolicy.PaidValue(sale, lines, b, 1m));
    }
}
