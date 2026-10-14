using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using Purch.Application.Catalog;
using Purch.Application.Auth;
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

    private static Task<HttpResponseMessage> DiscardAsync(HttpClient client, Guid id, string? pin)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/transactions/{id}/parked");
        if (pin is not null)
        {
            request.Content = JsonContent.Create(new VoidCartRequest(pin));
        }

        return client.SendAsync(request);
    }

    [Fact]
    public async Task Discard_WithItems_NeedsApproverPin_AndAuditsApprover()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        _ = await TestSessions.AddPersonAsync(admin, "Mae Manager", MembershipRole.Manager, StaffDuty.None, "567812");
        using var cashier = await TestSessions.CashierClientAsync(admin);
        var water = await CreateItemAsync(admin, "Water", 100m);
        var cart = await AddAsync(cashier, water);
        _ = await ParkAsync(cashier);

        var noPin = await DiscardAsync(cashier, cart.Id, null);
        Assert.True((int)noPin.StatusCode is >= 400 and < 500);
        _ = Assert.Single(await ListParkedAsync(cashier));

        var withPin = await DiscardAsync(cashier, cart.Id, "567812");
        Assert.Equal(HttpStatusCode.NoContent, withPin.StatusCode);
        Assert.Empty(await ListParkedAsync(cashier));
        var approved = await ScalarAsync<long>(
            "SELECT count(*) FROM \"AuditLogs\" WHERE \"TargetEntityId\" = @id AND \"ActionType\" = @type AND \"ApprovedByUserId\" IS NOT NULL",
            ("id", cart.Id), ("type", (int)AuditActionType.Void));
        Assert.Equal(1, approved);
    }

    [Fact]
    public async Task Sweeper_ExpiredCart_StaysOutOfVoidedTotals()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        var water = await CreateItemAsync(client, "Water", 100m);
        var stale = await AddAsync(client, water);
        _ = await ParkAsync(client);
        await ExecAsync("UPDATE \"Transactions\" SET \"ParkedAt\" = now() - interval '48 hours' WHERE \"Id\" = @id", ("id", stale.Id));

        using var scope = factory.Services.CreateScope();
        var parking = scope.ServiceProvider.GetRequiredService<ICartParkingService>();
        Assert.True(await parking.ExpireStaleAsync(DateTimeOffset.UtcNow) >= 1);

        var x = (await (await client.PostAsync("/reports/x-reading", null)).Content.ReadFromJsonAsync<Purch.Application.Reporting.BirReadingDto>(JsonOptions))!;
        Assert.Equal(0, x.VoidedCount);
        Assert.Equal((int)TransactionStatus.Voided, await ScalarAsync<int>("SELECT \"Status\" FROM \"Transactions\" WHERE \"Id\" = @id", ("id", stale.Id)));
    }

    [Fact]
    public async Task Resume_ModifierLine_RepricesAndUnavailableModifierDropsLine()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        var latte = await CreateItemAsync(client, "Latte", 100m);
        var group = (await (await client.PostAsJsonAsync("/modifier-groups", new CreateModifierGroupRequest("Add-ons", true, false))).Content.ReadFromJsonAsync<ModifierGroupDto>(JsonOptions))!;
        var modifier = (await (await client.PostAsJsonAsync($"/modifier-groups/{group.Id}/modifiers", new CreateItemModifierRequest("Shot", 20m))).Content.ReadFromJsonAsync<ModifierGroupDto>(JsonOptions))!.Modifiers[0];
        _ = await client.PostAsJsonAsync($"/items/{latte.Id}/modifier-groups", new AttachModifierGroupRequest(group.Id));
        var added = (await (await client.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(latte.Id, null, 1m, SelectedModifierIds: [modifier.Id]))).Content.ReadFromJsonAsync<TransactionDto>(JsonOptions))!;
        Assert.Equal(120m, added.TotalAmount);
        _ = await ParkAsync(client);
        await ExecAsync("UPDATE \"ItemModifiers\" SET \"PriceDelta\" = 30 WHERE \"Id\" = @id", ("id", modifier.Id));

        var parked = Assert.Single(await ListParkedAsync(client));
        var resumed = (await (await client.PostAsync($"/transactions/{parked.Id}/resume", null)).Content.ReadFromJsonAsync<TransactionDto>(JsonOptions))!;
        Assert.Equal(130m, resumed.TotalAmount);
        Assert.Contains(resumed.ChangedLines!, c => c.Reason == "PriceChanged");

        _ = await ParkAsync(client);
        await ExecAsync("UPDATE \"ItemModifiers\" SET \"IsActive\" = false WHERE \"Id\" = @id", ("id", modifier.Id));
        var again = (await (await client.PostAsync($"/transactions/{parked.Id}/resume", null)).Content.ReadFromJsonAsync<TransactionDto>(JsonOptions))!;
        Assert.Empty(again.Lines);
        Assert.Contains(again.ChangedLines!, c => c.Reason == "Unavailable");
    }

    [Fact]
    public async Task Resume_ComboLine_IsFlaggedNotRepricedAndUnavailableComponentDropsIt()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        var drinks = (await (await client.PostAsJsonAsync("/categories", new CreateCategoryRequest("Drinks", 1))).Content.ReadFromJsonAsync<CategoryDto>(JsonOptions))!;
        var soda = (await (await client.PostAsJsonAsync("/items", new CreateItemRequest("Soda", null, null, drinks.Id, 25m, null, PricingType.Unit))).Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;
        var combo = (await (await client.PostAsJsonAsync("/items", new CreateItemRequest("Meal", null, null, null, 150m, null, PricingType.Combo))).Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;
        var slot = (await (await client.PostAsJsonAsync($"/items/{combo.Id}/combo-components", new CreateItemComboComponentRequest(drinks.Id, "Drink", 1, null))).Content.ReadFromJsonAsync<ItemComboComponentDto>(JsonOptions))!;
        _ = await client.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(combo.Id, null, 1m, [new ComboSelectionRequest(slot.Id, soda.Id)]));
        _ = await ParkAsync(client);

        var parked = Assert.Single(await ListParkedAsync(client));
        var resumed = (await (await client.PostAsync($"/transactions/{parked.Id}/resume", null)).Content.ReadFromJsonAsync<TransactionDto>(JsonOptions))!;
        _ = Assert.Single(resumed.Lines);
        Assert.Contains(resumed.ChangedLines!, c => c.Reason == "NotRepriced");

        _ = await ParkAsync(client);
        await ExecAsync("UPDATE \"Items\" SET \"IsActive\" = false WHERE \"Id\" = @id", ("id", soda.Id));
        var again = (await (await client.PostAsync($"/transactions/{parked.Id}/resume", null)).Content.ReadFromJsonAsync<TransactionDto>(JsonOptions))!;
        Assert.Empty(again.Lines);
        Assert.Contains(again.ChangedLines!, c => c.Reason == "Unavailable");
    }

    [Fact]
    public async Task Discard_NoPin_IsExactly400_AndWrongPinsLockTheDevice()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        _ = await TestSessions.AddPersonAsync(admin, "Mae Manager", MembershipRole.Manager, StaffDuty.None, "567812");
        using var cashier = await TestSessions.CashierClientAsync(admin);
        var water = await CreateItemAsync(admin, "Water", 100m);
        var cart = await AddAsync(cashier, water);
        _ = await ParkAsync(cashier);

        Assert.Equal(HttpStatusCode.BadRequest, (await DiscardAsync(cashier, cart.Id, null)).StatusCode);

        // Five wrong PINs: each is a 400 and each failed attempt must persist, so the sixth try (even with the
        // right PIN) is locked out exactly as it is for VoidCart.
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await DiscardAsync(cashier, cart.Id, "999999")).StatusCode);
        }

        var locked = await DiscardAsync(cashier, cart.Id, "567812");
        Assert.Equal(HttpStatusCode.Forbidden, locked.StatusCode);
        _ = Assert.Single(await ListParkedAsync(cashier));
    }

    [Fact]
    public async Task Discard_ZeroTotal_NeedsNoPin()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        var water = await CreateItemAsync(client, "Water", 100m);
        var cart = await AddAsync(client, water);
        _ = await ParkAsync(client);
        await ExecAsync("UPDATE \"Transactions\" SET \"TotalAmount\" = 0 WHERE \"Id\" = @id", ("id", cart.Id));

        Assert.Equal(HttpStatusCode.NoContent, (await DiscardAsync(client, cart.Id, null)).StatusCode);
    }

    [Fact]
    public async Task XReading_CountsApprovedDiscard_ButNotSweptExpiredCart()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        _ = await TestSessions.AddPersonAsync(admin, "Mae Manager", MembershipRole.Manager, StaffDuty.None, "567812");
        using var cashier = await TestSessions.CashierClientAsync(admin);
        var water = await CreateItemAsync(admin, "Water", 100m);
        var soda = await CreateItemAsync(admin, "Soda", 40m);

        var discarded = await AddAsync(cashier, water);
        _ = await ParkAsync(cashier);
        var expired = await AddAsync(cashier, soda);
        _ = await ParkAsync(cashier);
        await ExecAsync("UPDATE \"Transactions\" SET \"ParkedAt\" = now() - interval '48 hours' WHERE \"Id\" = @id", ("id", expired.Id));

        Assert.Equal(HttpStatusCode.NoContent, (await DiscardAsync(cashier, discarded.Id, "567812")).StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<ICartParkingService>().ExpireStaleAsync(DateTimeOffset.UtcNow) >= 1);

        var x = (await (await admin.PostAsync("/reports/x-reading", null)).Content.ReadFromJsonAsync<Purch.Application.Reporting.BirReadingDto>(JsonOptions))!;
        Assert.Equal(1, x.VoidedCount);
        Assert.Equal(100m, x.VoidedAmount);
    }

    [Fact]
    public async Task Resume_OtherBranchAndNoLongerParked_Is403Not409()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        var shop = TestSessions.ShopOf(admin);
        var water = await CreateItemAsync(admin, "Water", 100m);
        var cart = await AddAsync(admin, water);
        _ = await ParkAsync(admin);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/transactions/{cart.Id}/resume", null)).StatusCode);

        var branch = (await (await admin.PostAsJsonAsync("/branches", new CreateBranchRequest("Second Branch", null))).Content.ReadFromJsonAsync<BranchDto>(JsonOptions))!;
        using var anonymous = factory.CreateClient();
        var (_, credential) = await TestSessions.PairAsync(admin, anonymous, branch.Id, DeviceType.Register, "Other Till");
        using var other = await TestSessions.UnlockAsync(admin, shop.Tenant.AdminMembershipId, TestSessions.AdminPin, credential);

        Assert.Equal(HttpStatusCode.Forbidden, (await other.PostAsync($"/transactions/{cart.Id}/resume", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync($"/transactions/{cart.Id}/resume", null)).StatusCode);
    }

    private sealed class NoTenant : ICurrentTenantProvider
    {
        public Guid? TenantId => null;
    }
}
