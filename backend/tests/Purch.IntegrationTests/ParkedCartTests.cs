using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using Purch.Application.Catalog;
using Purch.Application.Common;
using Purch.Application.Devices;
using Purch.Application.Onboarding;
using Purch.Application.Pos;
using Purch.Domain.Enums;
using Purch.Infrastructure.Persistence;
using Purch.Infrastructure.Retention;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class ParkedCartTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static async Task<ItemDto> CreateItemAsync(HttpClient client, string name, decimal price)
    {
        var response = await client.PostAsJsonAsync("/items", new CreateItemRequest(name, null, null, null, price, null, PricingType.Unit));
        Assert.True(response.IsSuccessStatusCode);
        return (await response.Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;
    }

    private static async Task<TransactionDto> AddAsync(HttpClient client, ItemDto item, decimal quantity = 1m)
    {
        var response = await client.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(item.Id, null, quantity));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions))!;
    }

    private static async Task<HttpResponseMessage> ParkAsync(HttpClient client, string? label = null)
        => await client.PostAsJsonAsync("/transactions/cart/park", new ParkCartRequest(label));

    private static async Task<List<ParkedCartSummaryDto>> ListParkedAsync(HttpClient client)
        => (await client.GetFromJsonAsync<List<ParkedCartSummaryDto>>("/transactions/parked", JsonOptions))!;

    private async Task ExecAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        _ = await command.ExecuteNonQueryAsync();
    }

    private async Task<T> ScalarAsync<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return (T)(await command.ExecuteScalarAsync())!;
    }

    [Fact]
    public async Task Park_MovesOpenCartToParkedAndAllowsNewCart()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        var item = await CreateItemAsync(client, "Water", 100m);
        var original = await AddAsync(client, item);

        var park = await ParkAsync(client, "Table 4");
        Assert.Equal(HttpStatusCode.NoContent, park.StatusCode);

        var fresh = (await client.GetFromJsonAsync<TransactionDto>("/transactions/cart", JsonOptions))!;
        Assert.NotEqual(original.Id, fresh.Id);
        Assert.Empty(fresh.Lines);

        var parked = Assert.Single(await ListParkedAsync(client));
        Assert.Equal(original.Id, parked.Id);
        Assert.Equal("Table 4", parked.Label);
        Assert.Equal(1, parked.LineCount);
        Assert.Equal(100m, parked.TotalAmount);
    }

    [Fact]
    public async Task Park_EmptyCart_Returns400()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        _ = await client.GetAsync("/transactions/cart");

        var park = await ParkAsync(client);

        Assert.Equal(HttpStatusCode.BadRequest, park.StatusCode);
    }

    [Fact]
    public async Task Resume_SwapsWithNonEmptyOpenCart()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        var water = await CreateItemAsync(client, "Water", 100m);
        var soda = await CreateItemAsync(client, "Soda", 50m);
        var first = await AddAsync(client, water);
        _ = await ParkAsync(client, "First");
        var second = await AddAsync(client, soda);

        var resume = await client.PostAsync($"/transactions/{first.Id}/resume", null);

        Assert.Equal(HttpStatusCode.OK, resume.StatusCode);
        var resumed = (await resume.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions))!;
        Assert.Equal(first.Id, resumed.Id);
        Assert.Equal(TransactionStatus.Open, resumed.Status);
        var parked = Assert.Single(await ListParkedAsync(client));
        Assert.Equal(second.Id, parked.Id);
        var current = (await client.GetFromJsonAsync<TransactionDto>("/transactions/cart", JsonOptions))!;
        Assert.Equal(first.Id, current.Id);
    }

    [Fact]
    public async Task Resume_DiscardsEmptyOpenCart()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        var water = await CreateItemAsync(client, "Water", 100m);
        var first = await AddAsync(client, water);
        _ = await ParkAsync(client);
        var empty = (await client.GetFromJsonAsync<TransactionDto>("/transactions/cart", JsonOptions))!;

        var resume = await client.PostAsync($"/transactions/{first.Id}/resume", null);

        Assert.Equal(HttpStatusCode.OK, resume.StatusCode);
        Assert.Empty(await ListParkedAsync(client));
        var status = await ScalarAsync<int>("SELECT \"Status\" FROM \"Transactions\" WHERE \"Id\" = @id", ("id", empty.Id));
        Assert.Equal((int)TransactionStatus.Voided, status);
    }

    [Fact]
    public async Task Resume_OnOtherBranch_Returns403()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        var shop = TestSessions.ShopOf(admin);
        var water = await CreateItemAsync(admin, "Water", 100m);
        var cart = await AddAsync(admin, water);
        _ = await ParkAsync(admin);

        var branchResponse = await admin.PostAsJsonAsync("/branches", new CreateBranchRequest("Second Branch", null));
        var branch = (await branchResponse.Content.ReadFromJsonAsync<BranchDto>(JsonOptions))!;
        using var anonymous = factory.CreateClient();
        var (_, credential) = await TestSessions.PairAsync(admin, anonymous, branch.Id, DeviceType.Register, "Other Till");
        using var other = await TestSessions.UnlockAsync(admin, shop.Tenant.AdminMembershipId, TestSessions.AdminPin, credential);

        var resume = await other.PostAsync($"/transactions/{cart.Id}/resume", null);

        Assert.Equal(HttpStatusCode.Forbidden, resume.StatusCode);
    }

    [Fact]
    public async Task Resume_Concurrent_ExactlyOneSucceeds()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var first = await TestSessions.AdminClientAsync(factory);
        using var second = await TestSessions.AdminOnNewRegisterAsync(first);
        var water = await CreateItemAsync(first, "Water", 100m);
        var cart = await AddAsync(first, water);
        _ = await ParkAsync(first);

        var results = await Task.WhenAll(
            first.PostAsync($"/transactions/{cart.Id}/resume", null),
            second.PostAsync($"/transactions/{cart.Id}/resume", null));

        var codes = results.Select(r => r.StatusCode).OrderBy(c => (int)c).ToList();
        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.Conflict], codes);
    }

    [Fact]
    public async Task Resume_RepricesAndReportsChangedLines()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        var repriced = await CreateItemAsync(client, "Repriced", 100m);
        var gone = await CreateItemAsync(client, "Gone", 50m);
        var same = await CreateItemAsync(client, "Same", 30m);
        _ = await AddAsync(client, repriced);
        _ = await AddAsync(client, gone);
        var before = await AddAsync(client, same);
        _ = await ParkAsync(client);
        var repricedLine = before.Lines.Single(l => l.ItemId == repriced.Id).Id;
        var goneLine = before.Lines.Single(l => l.ItemId == gone.Id).Id;

        await ExecAsync("UPDATE \"Items\" SET \"BasePrice\" = 120 WHERE \"Id\" = @id", ("id", repriced.Id));
        await ExecAsync("UPDATE \"Items\" SET \"IsActive\" = false WHERE \"Id\" = @id", ("id", gone.Id));

        var parked = Assert.Single(await ListParkedAsync(client));
        var resume = await client.PostAsync($"/transactions/{parked.Id}/resume", null);

        Assert.Equal(HttpStatusCode.OK, resume.StatusCode);
        var cart = (await resume.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions))!;
        Assert.Equal(2, cart.Lines.Count);
        Assert.Equal(150m, cart.TotalAmount);
        Assert.NotNull(cart.ChangedLines);
        Assert.Contains(cart.ChangedLines!, c => c.LineId == repricedLine && c.Reason == "PriceChanged");
        Assert.Contains(cart.ChangedLines!, c => c.LineId == goneLine && c.Reason == "Unavailable");
        Assert.Equal(2, cart.ChangedLines!.Count);
    }

    [Fact]
    public async Task Checkout_DoesNotVoidParkedCarts()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        var water = await CreateItemAsync(client, "Water", 100m);
        var parkedCart = await AddAsync(client, water);
        _ = await ParkAsync(client);

        var checkout = await client.PostAsJsonAsync(
            "/transactions/checkout",
            new CheckoutRequest(
                Guid.NewGuid(),
                [new AddTransactionLineRequest(water.Id, null, 1m)],
                SeniorPwdDiscountApplied: false,
                PromoCode: null,
                OrderType: "Take Out",
                Payment: new RecordPaymentRequest(PaymentMethod.Cash, 1000m),
                ExpectedTotal: 100m));

        Assert.Equal(HttpStatusCode.OK, checkout.StatusCode);
        var parked = Assert.Single(await ListParkedAsync(client));
        Assert.Equal(parkedCart.Id, parked.Id);
        var status = await ScalarAsync<int>("SELECT \"Status\" FROM \"Transactions\" WHERE \"Id\" = @id", ("id", parkedCart.Id));
        Assert.Equal((int)TransactionStatus.Parked, status);
    }

    [Fact]
    public async Task Sweeper_VoidsParkedPastTtlWithAudit()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        var water = await CreateItemAsync(client, "Water", 100m);
        var stale = await AddAsync(client, water);
        _ = await ParkAsync(client, "Stale");
        var fresh = await AddAsync(client, water);
        _ = await ParkAsync(client, "Fresh");
        await ExecAsync("UPDATE \"Transactions\" SET \"ParkedAt\" = now() - interval '48 hours' WHERE \"Id\" = @id", ("id", stale.Id));

        using var scope = factory.Services.CreateScope();
        var parking = scope.ServiceProvider.GetRequiredService<ICartParkingService>();
        var options = new DbContextOptionsBuilder<PurchDbContext>().UseNpgsql(postgres.ConnectionString).Options;
        await using var db = new PurchDbContext(options, new NoTenant());
        var sweeper = new RetentionSweeper(db, Options.Create(new RetentionOptions()), parking);

        var result = await sweeper.RunAsync();

        Assert.True(result.ParkedCartsExpired >= 1);
        Assert.Equal((int)TransactionStatus.Voided, await ScalarAsync<int>("SELECT \"Status\" FROM \"Transactions\" WHERE \"Id\" = @id", ("id", stale.Id)));
        Assert.Equal((int)TransactionStatus.Parked, await ScalarAsync<int>("SELECT \"Status\" FROM \"Transactions\" WHERE \"Id\" = @id", ("id", fresh.Id)));
        var audits = await ScalarAsync<long>(
            "SELECT count(*) FROM \"AuditLogs\" WHERE \"TargetEntityId\" = @id AND \"ActionType\" = @type",
            ("id", stale.Id), ("type", (int)AuditActionType.CartExpired));
        Assert.Equal(1, audits);

        // A second sweep finds nothing left to expire for it: no double void.
        var again = await sweeper.RunAsync();
        Assert.Equal(1, await ScalarAsync<long>(
            "SELECT count(*) FROM \"AuditLogs\" WHERE \"TargetEntityId\" = @id AND \"ActionType\" = @type",
            ("id", stale.Id), ("type", (int)AuditActionType.CartExpired)));
        _ = again;
    }

    [Fact]
    public async Task Park_KioskClaimedOrder_Returns400()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        var shop = TestSessions.ShopOf(admin);
        using var anonymous = factory.CreateClient();
        var (_, kioskCredential) = await TestSessions.PairAsync(admin, anonymous, shop.Tenant.BranchId, DeviceType.Kiosk, "Kiosk");
        var session = await anonymous.PostAsJsonAsync("/devices/session", new DeviceSessionRequest(kioskCredential));
        using var kiosk = TestSessions.Bearer(factory, (await session.Content.ReadFromJsonAsync<DeviceSessionDto>(JsonOptions))!.AccessToken!);
        var item = await CreateItemAsync(admin, "Ramen", 150m);
        _ = await kiosk.PostAsJsonAsync("/kiosk/cart/lines", new AddTransactionLineRequest(item.Id, null, 1m));
        var submitted = (await (await kiosk.PostAsync("/kiosk/cart/submit", null)).Content.ReadFromJsonAsync<TransactionDto>(JsonOptions))!;
        var claim = await admin.PostAsync($"/transactions/kiosk-pending/{submitted.Id}/claim", null);
        Assert.Equal(HttpStatusCode.OK, claim.StatusCode);

        var park = await ParkAsync(admin);

        Assert.Equal(HttpStatusCode.BadRequest, park.StatusCode);
    }

    private sealed class NoTenant : ICurrentTenantProvider
    {
        public Guid? TenantId => null;
    }
}
