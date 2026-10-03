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
public sealed class AdjustmentEndpointsTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static async Task<(ItemDto Ramen, ItemDto Katsudon, TransactionDto Sale)> RamenAndKatsudonSaleAsync(HttpClient client, decimal katsudonPrice = 100m)
    {
        var ramen = (await (await client.PostAsJsonAsync("/items", new CreateItemRequest("Ramen", null, null, null, 100m, null, PricingType.Unit))).Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;
        var katsudon = (await (await client.PostAsJsonAsync("/items", new CreateItemRequest("Katsudon", null, null, null, katsudonPrice, null, PricingType.Unit))).Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;

        var checkoutResponse = await client.PostAsJsonAsync(
            "/transactions/checkout",
            new CheckoutRequest(
                Guid.NewGuid(),
                [new AddTransactionLineRequest(ramen.Id, null, 2m)],
                false, null, null,
                new RecordPaymentRequest(PaymentMethod.Cash, 500m)));
        var sale = (await checkoutResponse.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions))!;
        return (ramen, katsudon, sale);
    }

    [Fact]
    public async Task An_even_exchange_needs_no_settlement_and_moves_stock_both_ways()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var (ramen, katsudon, sale) = await RamenAndKatsudonSaleAsync(client);
        var ramenLine = sale.Lines.Single(l => l.ItemId == ramen.Id);

        var response = await client.PostAsJsonAsync(
            $"/transactions/{sale.Id}/exchange",
            new CreateExchangeRequest(
                [new ReturnLineRequest(ramenLine.Id, 1m)],
                [new ReplacementLineRequest(katsudon.Id, null, 1m)],
                "Customer wanted katsudon instead",
                "1234"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var adjustment = await response.Content.ReadFromJsonAsync<AdjustmentDto>(JsonOptions);
        Assert.Equal(100m, adjustment!.ReturnedTotal);
        Assert.Equal(100m, adjustment.ReplacementTotal);
        Assert.Equal(0m, adjustment.PriceDifference);
        Assert.Null(adjustment.SettlementMethod);
        Assert.Null(adjustment.ChangeGiven);
        Assert.Equal(sale.ReceiptNumber, adjustment.OriginalReceiptNumber);

        // Both items started at 0 stock; the sale took 2 ramen off (-2), the exchange gives 1 back
        // (-1) and takes 1 katsudon off (-1).
        var items = await client.GetFromJsonAsync<List<ItemDto>>("/items", JsonOptions);
        Assert.Equal(-1m, items!.Single(i => i.Id == ramen.Id).StockOnHand);
        Assert.Equal(-1m, items!.Single(i => i.Id == katsudon.Id).StockOnHand);
    }

    [Fact]
    public async Task Exchanging_for_a_pricier_item_needs_cash_covering_the_difference_and_gives_change()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var (ramen, katsudon, sale) = await RamenAndKatsudonSaleAsync(client, katsudonPrice: 130m);
        var ramenLine = sale.Lines.Single(l => l.ItemId == ramen.Id);

        var refused = await client.PostAsJsonAsync(
            $"/transactions/{sale.Id}/exchange",
            new CreateExchangeRequest([new ReturnLineRequest(ramenLine.Id, 1m)], [new ReplacementLineRequest(katsudon.Id, null, 1m)], "Upgrade", "1234"));
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);

        var response = await client.PostAsJsonAsync(
            $"/transactions/{sale.Id}/exchange",
            new CreateExchangeRequest(
                [new ReturnLineRequest(ramenLine.Id, 1m)],
                [new ReplacementLineRequest(katsudon.Id, null, 1m)],
                "Upgrade",
                "1234",
                PaymentMethod.Cash,
                50m));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var adjustment = await response.Content.ReadFromJsonAsync<AdjustmentDto>(JsonOptions);
        Assert.Equal(30m, adjustment!.PriceDifference);
        Assert.Equal(20m, adjustment.ChangeGiven);
    }

    [Fact]
    public async Task Exchanging_for_a_cheaper_item_is_a_refund_and_needs_no_cash_tendered()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var (ramen, katsudon, sale) = await RamenAndKatsudonSaleAsync(client, katsudonPrice: 70m);
        var ramenLine = sale.Lines.Single(l => l.ItemId == ramen.Id);

        var response = await client.PostAsJsonAsync(
            $"/transactions/{sale.Id}/exchange",
            new CreateExchangeRequest(
                [new ReturnLineRequest(ramenLine.Id, 1m)],
                [new ReplacementLineRequest(katsudon.Id, null, 1m)],
                "Customer wanted something cheaper",
                "1234",
                PaymentMethod.Cash));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var adjustment = await response.Content.ReadFromJsonAsync<AdjustmentDto>(JsonOptions);
        Assert.Equal(-30m, adjustment!.PriceDifference);
        Assert.Null(adjustment.ChangeGiven);
    }

    [Fact]
    public async Task An_exchange_without_an_approver_pin_is_refused_and_nothing_moves()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var (ramen, katsudon, sale) = await RamenAndKatsudonSaleAsync(client);
        var ramenLine = sale.Lines.Single(l => l.ItemId == ramen.Id);

        var response = await client.PostAsJsonAsync(
            $"/transactions/{sale.Id}/exchange",
            new CreateExchangeRequest([new ReturnLineRequest(ramenLine.Id, 1m)], [new ReplacementLineRequest(katsudon.Id, null, 1m)], "No PIN given", null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Items start at 0 stock; the sale alone took 2 ramen off (-2) and nothing else moved
        // since the exchange was refused.
        var items = await client.GetFromJsonAsync<List<ItemDto>>("/items", JsonOptions);
        Assert.Equal(-2m, items!.Single(i => i.Id == ramen.Id).StockOnHand);
    }

    [Fact]
    public async Task A_second_exchange_cannot_return_more_of_a_line_than_is_left()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var (ramen, katsudon, sale) = await RamenAndKatsudonSaleAsync(client);
        var ramenLine = sale.Lines.Single(l => l.ItemId == ramen.Id);

        var first = await client.PostAsJsonAsync(
            $"/transactions/{sale.Id}/exchange",
            new CreateExchangeRequest([new ReturnLineRequest(ramenLine.Id, 1m)], [new ReplacementLineRequest(katsudon.Id, null, 1m)], "First exchange", "1234"));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Only 1 ramen is left on that line (2 bought, 1 already returned).
        var second = await client.PostAsJsonAsync(
            $"/transactions/{sale.Id}/exchange",
            new CreateExchangeRequest([new ReturnLineRequest(ramenLine.Id, 2m)], [new ReplacementLineRequest(katsudon.Id, null, 2m)], "Trying to return too much", "1234"));

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);

        var thirdWithinLimit = await client.PostAsJsonAsync(
            $"/transactions/{sale.Id}/exchange",
            new CreateExchangeRequest([new ReturnLineRequest(ramenLine.Id, 1m)], [new ReplacementLineRequest(katsudon.Id, null, 1m)], "Second exchange, within what's left", "1234"));
        Assert.Equal(HttpStatusCode.OK, thirdWithinLimit.StatusCode);
    }

    [Fact]
    public async Task Returnable_lines_shrink_as_exchanges_take_quantity_back()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var (ramen, katsudon, sale) = await RamenAndKatsudonSaleAsync(client);
        var ramenLine = sale.Lines.Single(l => l.ItemId == ramen.Id);

        var before = await client.GetFromJsonAsync<List<ReturnableLineDto>>($"/transactions/{sale.Id}/returnable-lines", JsonOptions);
        Assert.Equal(2m, before!.Single(l => l.LineId == ramenLine.Id).RemainingQuantity);

        var exchange = await client.PostAsJsonAsync(
            $"/transactions/{sale.Id}/exchange",
            new CreateExchangeRequest([new ReturnLineRequest(ramenLine.Id, 1m)], [new ReplacementLineRequest(katsudon.Id, null, 1m)], "Swap one", "1234"));
        Assert.Equal(HttpStatusCode.OK, exchange.StatusCode);

        var after = await client.GetFromJsonAsync<List<ReturnableLineDto>>($"/transactions/{sale.Id}/returnable-lines", JsonOptions);
        Assert.Equal(1m, after!.Single(l => l.LineId == ramenLine.Id).RemainingQuantity);
    }

    [Fact]
    public async Task A_combo_or_service_item_cannot_be_taken_as_a_replacement_yet()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var (ramen, _, sale) = await RamenAndKatsudonSaleAsync(client);
        var ramenLine = sale.Lines.Single(l => l.ItemId == ramen.Id);
        var haircut = (await (await client.PostAsJsonAsync("/items", new CreateItemRequest("Haircut", null, null, null, 200m, null, PricingType.Service))).Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;

        var response = await client.PostAsJsonAsync(
            $"/transactions/{sale.Id}/exchange",
            new CreateExchangeRequest([new ReturnLineRequest(ramenLine.Id, 1m)], [new ReplacementLineRequest(haircut.Id, null, 1m)], "Not supported yet", "1234"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_open_cart_cannot_be_exchanged_against()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var item = (await (await client.PostAsJsonAsync("/items", new CreateItemRequest("Water", null, null, null, 15m, null, PricingType.Unit))).Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;
        var cart = (await (await client.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(item.Id, null, 1m))).Content.ReadFromJsonAsync<TransactionDto>(JsonOptions))!;
        var line = cart.Lines.Single();

        var response = await client.PostAsJsonAsync(
            $"/transactions/{cart.Id}/exchange",
            new CreateExchangeRequest([new ReturnLineRequest(line.Id, 1m)], [new ReplacementLineRequest(item.Id, null, 1m)], "Still open", "1234"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_exchange_leaves_an_audit_entry_naming_the_approver()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var (ramen, katsudon, sale) = await RamenAndKatsudonSaleAsync(client);
        var ramenLine = sale.Lines.Single(l => l.ItemId == ramen.Id);

        _ = await client.PostAsJsonAsync(
            $"/transactions/{sale.Id}/exchange",
            new CreateExchangeRequest([new ReturnLineRequest(ramenLine.Id, 1m)], [new ReplacementLineRequest(katsudon.Id, null, 1m)], "Audited exchange", "1234"));

        var logs = await client.GetFromJsonAsync<List<AuditLogDto>>("/audit-logs", JsonOptions);
        var entry = Assert.Single(logs!, log => log.ActionType == AuditActionType.Exchange);
        Assert.Equal(sale.Id, entry.TargetEntityId);
    }

    private static Task<HttpClient> AuthenticatedAdminClientAsync(PurchApiFactory factory) => TestSessions.AdminClientAsync(factory);

}
