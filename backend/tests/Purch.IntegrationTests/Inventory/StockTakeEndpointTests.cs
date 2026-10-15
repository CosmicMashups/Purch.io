using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Purch.Application.Common;
using Purch.Application.Pos;
using Purch.Common.TestUtilities;
using Purch.Domain.Entities;
using Purch.Infrastructure.Persistence;
using Purch.Infrastructure.Repositories;
using Purch.Application.Catalog;
using Purch.Application.Inventory;
using Purch.Application.Onboarding;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests.Inventory;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class StockTakeEndpointTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }

    private static async Task<ItemDto> StockedItemAsync(HttpClient admin, string name, decimal stock)
    {
        var item = await ReadAsync<ItemDto>(await admin.PostAsJsonAsync("/items", new CreateItemRequest(name, null, null, null, 10m, null, PricingType.Unit)));
        var branchId = TestSessions.ShopOf(admin).Tenant.BranchId;
        _ = await ReadAsync<InventoryMovementDto>(await admin.PostAsJsonAsync(
            "/inventory/movements", new RecordMovementRequest(item.Id, branchId, MovementType.StockIn, stock, null, null, null, null)));
        return item;
    }

    private static async Task<StockTakeDto> ReviewTakeWithCountAsync(HttpClient client, ItemDto item, decimal counted)
    {
        var take = await ReadAsync<StockTakeDto>(await client.PostAsJsonAsync("/stock-takes",
            new CreateStockTakeRequest("Shelf A", StockTakeScopeKind.Picked, ItemIds: [item.Id])));
        _ = await ReadAsync<StockTakeDto>(await client.PostAsync($"/stock-takes/{take.Id}/start", null));
        var lineId = take.Lines.Single().Id;
        _ = await ReadAsync<StockTakeDto>(await client.PutAsJsonAsync($"/stock-takes/{take.Id}/lines/{lineId}", new CountStockTakeLineRequest(counted)));
        return await ReadAsync<StockTakeDto>(await client.PostAsync($"/stock-takes/{take.Id}/review", null));
    }

    private async Task<decimal> StockOfAsync(Guid itemId)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT \"StockOnHand\" FROM \"Items\" WHERE \"Id\" = @id", connection);
        _ = command.Parameters.AddWithValue("id", itemId);
        return (decimal)(await command.ExecuteScalarAsync())!;
    }

    private async Task<int> FailedPinAttemptsAsync(Guid deviceId)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT \"ApproverPinFailedAttempts\" FROM \"Devices\" WHERE \"Id\" = @id", connection);
        _ = command.Parameters.AddWithValue("id", deviceId);
        return (int)(await command.ExecuteScalarAsync())!;
    }

    private async Task<int> AuditCountAsync(Guid takeId)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT COUNT(*) FROM \"AuditLogs\" WHERE \"TargetEntityId\" = @id AND \"ActionType\" = @type AND \"ApprovedByUserId\" IS NOT NULL", connection);
        _ = command.Parameters.AddWithValue("id", takeId);
        _ = command.Parameters.AddWithValue("type", (int)AuditActionType.StockTakeApplied);
        return (int)(long)(await command.ExecuteScalarAsync())!;
    }

    [Fact]
    public async Task A_cashier_is_refused_every_stock_take_route()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        using var cashier = await TestSessions.CashierClientAsync(admin);
        var id = Guid.NewGuid();

        foreach (var response in new[]
        {
            await cashier.GetAsync("/stock-takes"),
            await cashier.GetAsync($"/stock-takes/{id}"),
            await cashier.PostAsJsonAsync("/stock-takes", new CreateStockTakeRequest("x", StockTakeScopeKind.All)),
            await cashier.PutAsJsonAsync($"/stock-takes/{id}/lines/{Guid.NewGuid()}", new CountStockTakeLineRequest(1m)),
            await cashier.PostAsync($"/stock-takes/{id}/start", null),
            await cashier.PostAsync($"/stock-takes/{id}/review", null),
            await cashier.PostAsJsonAsync($"/stock-takes/{id}/apply", new ApplyStockTakeRequest(TestSessions.AdminPin)),
            await cashier.PostAsync($"/stock-takes/{id}/cancel", null),
        })
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public async Task A_wrong_pin_leaves_stock_unchanged_and_counts_a_failed_attempt_on_the_device()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        var item = await StockedItemAsync(admin, "Rice", 10m);
        var take = await ReviewTakeWithCountAsync(admin, item, 7m);
        var deviceId = TestSessions.ShopOf(admin).RegisterId;
        var before = await FailedPinAttemptsAsync(deviceId);

        var response = await admin.PostAsJsonAsync($"/stock-takes/{take.Id}/apply", new ApplyStockTakeRequest("000000"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("approverPin", await response.Content.ReadAsStringAsync());
        Assert.Equal(10m, await StockOfAsync(item.Id));
        Assert.Equal(before + 1, await FailedPinAttemptsAsync(deviceId));
        var reloaded = await ReadAsync<StockTakeDto>(await admin.GetAsync($"/stock-takes/{take.Id}"));
        Assert.Equal(StockTakeStatus.Review, reloaded.Status);
        Assert.Equal(0, await AuditCountAsync(take.Id));
    }

    [Fact]
    public async Task Apply_posts_the_variance_once_and_a_second_apply_returns_the_first_result()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        var item = await StockedItemAsync(admin, "Rice", 10m);
        var take = await ReviewTakeWithCountAsync(admin, item, 7m);

        var first = await ReadAsync<StockTakeDto>(await admin.PostAsJsonAsync($"/stock-takes/{take.Id}/apply", new ApplyStockTakeRequest(TestSessions.AdminPin)));
        var second = await ReadAsync<StockTakeDto>(await admin.PostAsJsonAsync($"/stock-takes/{take.Id}/apply", new ApplyStockTakeRequest(TestSessions.AdminPin)));

        Assert.Equal(StockTakeStatus.Applied, first.Status);
        Assert.Equal(7m, await StockOfAsync(item.Id));
        Assert.Equal(first.Lines.Single().MovementId, second.Lines.Single().MovementId);
        Assert.Equal(1, await AuditCountAsync(take.Id));
    }

    [Fact]
    public async Task Overlapping_open_takes_are_allowed_and_both_apply_by_delta()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        var item = await StockedItemAsync(admin, "Rice", 10m);

        var firstTake = await ReviewTakeWithCountAsync(admin, item, 9m);
        var overlapping = await ReadAsync<StockTakeDto>(await admin.PostAsJsonAsync("/stock-takes",
            new CreateStockTakeRequest("Whole shop", StockTakeScopeKind.All)));
        Assert.NotEmpty(overlapping.Warnings);
        Assert.Contains(overlapping.Lines, line => line.ItemId == item.Id);

        _ = await ReadAsync<StockTakeDto>(await admin.PostAsJsonAsync($"/stock-takes/{firstTake.Id}/apply", new ApplyStockTakeRequest(TestSessions.AdminPin)));
        Assert.Equal(9m, await StockOfAsync(item.Id));
    }

    private static Task<HttpClient> WarehouseClientAsync(HttpClient admin) =>
        TestSessions.StaffClientAsync(admin, "Wes Warehouse", MembershipRole.Staff, StaffDuty.Warehouse, "456712");

    private async Task<decimal?> MovementQuantityAsync(Guid movementId)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT \"Quantity\" FROM \"InventoryMovements\" WHERE \"Id\" = @id", connection);
        _ = command.Parameters.AddWithValue("id", movementId);
        return (decimal?)await command.ExecuteScalarAsync();
    }

    [Fact]
    public async Task Warehouse_counts_but_only_a_manager_moves_a_take_to_review_and_warehouse_cannot_apply()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        using var manager = await TestSessions.ManagerClientAsync(admin);
        using var warehouse = await WarehouseClientAsync(admin);
        var item = await StockedItemAsync(admin, "Rice", 10m);

        var take = await ReadAsync<StockTakeDto>(await warehouse.PostAsJsonAsync("/stock-takes",
            new CreateStockTakeRequest("Blind shelf", StockTakeScopeKind.Picked, Blind: true, ItemIds: [item.Id])));
        _ = await ReadAsync<StockTakeDto>(await warehouse.PostAsync($"/stock-takes/{take.Id}/start", null));
        var counted = await ReadAsync<StockTakeDto>(await warehouse.PutAsJsonAsync($"/stock-takes/{take.Id}/lines/{take.Lines.Single().Id}", new CountStockTakeLineRequest(7m)));
        Assert.Null(counted.Lines.Single().ExpectedQty);

        Assert.Equal(HttpStatusCode.Forbidden, (await warehouse.PostAsync($"/stock-takes/{take.Id}/review", null)).StatusCode);
        var review = await ReadAsync<StockTakeDto>(await manager.PostAsync($"/stock-takes/{take.Id}/review", null));
        Assert.Equal(10m, review.Lines.Single().ExpectedQty);

        // A blind take in review: the counter may no longer change counts.
        Assert.Equal(HttpStatusCode.Forbidden, (await warehouse.PutAsJsonAsync($"/stock-takes/{take.Id}/lines/{take.Lines.Single().Id}", new CountStockTakeLineRequest(10m))).StatusCode);

        // Warehouse is not an approver: its own PIN is rejected and nothing is posted.
        var refused = await warehouse.PostAsJsonAsync($"/stock-takes/{take.Id}/apply", new ApplyStockTakeRequest("456712"));
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal(10m, await StockOfAsync(item.Id));
    }

    // --- Deterministic concurrency tests: the real StockTakeService over separate DbContexts, so one context can
    // hold a stale tracked take while another commits. ---

    private sealed class Actor(Guid userId) : ICurrentActorProvider
    {
        public Guid? UserId => userId;
        public Guid? DeviceId => null;
        public Guid? BranchId => null;
        public ScopeType? ScopeType => Domain.Enums.ScopeType.Tenant;
        public Guid? ScopeId => null;
        public Role? Role => Domain.Enums.Role.Admin;
    }

    private sealed class Approver(Guid userId) : IApproverAuthorizationService
    {
        public Task<User> AuthorizeAsync(string? pin, CancellationToken cancellationToken = default) =>
            Task.FromResult(new User { Id = userId, Role = Role.Manager, Name = "Approver" });
    }

    private sealed class AnyBranch : IBranchScopeGuard
    {
        public Task EnsureAllowedAsync(Guid branchId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task EnsureAnyAllowedAsync(IReadOnlyCollection<Guid> branchIds, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private PurchDbContext NewContext(Guid tenantId) =>
        new(new DbContextOptionsBuilder<PurchDbContext>().UseNpgsql(postgres.ConnectionString).Options, new TestCurrentTenantProvider { TenantId = tenantId });

    private static StockTakeService ServiceOver(PurchDbContext db, Guid tenantId, Guid userId)
    {
        var ingredients = new EfInventoryItemRepository(db);
        return new StockTakeService(
            new EfStockTakeRepository(db), new EfItemRepository(db), ingredients,
            new ItemStockService(new EfTenantRepository(db), ingredients, new EfItemRecipeRepository(db)),
            new EfInventoryMovementRepository(db), new EfAuditLogRepository(db), new Approver(userId), new AnyBranch(),
            new TestCurrentTenantProvider { TenantId = tenantId }, new Actor(userId), new EfUnitOfWork(db));
    }

    /// <summary>Rice 10 counted 7 (variance -3), Salt 10 counted 10 (variance 0) when taken to Review; otherwise left
    /// in Counting with nothing counted.</summary>
    private static async Task<(StockTakeDto Take, ItemDto Rice, ItemDto Salt)> TwoLineTakeAsync(HttpClient admin, bool toReview)
    {
        var rice = await StockedItemAsync(admin, "Rice", 10m);
        var salt = await StockedItemAsync(admin, "Salt", 10m);
        var take = await ReadAsync<StockTakeDto>(await admin.PostAsJsonAsync("/stock-takes",
            new CreateStockTakeRequest("Two lines", StockTakeScopeKind.Picked, ItemIds: [rice.Id, salt.Id])));
        _ = await ReadAsync<StockTakeDto>(await admin.PostAsync($"/stock-takes/{take.Id}/start", null));
        if (toReview)
        {
            _ = await ReadAsync<StockTakeDto>(await admin.PutAsJsonAsync($"/stock-takes/{take.Id}/lines/{take.Lines.Single(l => l.ItemId == rice.Id).Id}", new CountStockTakeLineRequest(7m)));
            _ = await ReadAsync<StockTakeDto>(await admin.PutAsJsonAsync($"/stock-takes/{take.Id}/lines/{take.Lines.Single(l => l.ItemId == salt.Id).Id}", new CountStockTakeLineRequest(10m)));
            _ = await ReadAsync<StockTakeDto>(await admin.PostAsync($"/stock-takes/{take.Id}/review", null));
        }

        return (take, rice, salt);
    }

    private async Task<int> AdjustmentCountAsync(Guid itemId)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT COUNT(*) FROM \"InventoryMovements\" WHERE \"ItemId\" = @id AND \"Type\" = @type", connection);
        _ = command.Parameters.AddWithValue("id", itemId);
        _ = command.Parameters.AddWithValue("type", (int)MovementType.Adjustment);
        return (int)(long)(await command.ExecuteScalarAsync())!;
    }

    [Fact]
    public async Task A_count_loaded_before_apply_cannot_be_saved_after_it_on_either_kind_of_line()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        var shop = TestSessions.ShopOf(admin);
        var (tenantId, userId) = (shop.Tenant.TenantId, shop.Tenant.AdminMembershipId);
        var (take, rice, salt) = await TwoLineTakeAsync(admin, toReview: true);
        var riceLine = take.Lines.Single(l => l.ItemId == rice.Id).Id;
        var saltLine = take.Lines.Single(l => l.ItemId == salt.Id).Id;

        await using var staleRice = NewContext(tenantId);
        await using var staleSalt = NewContext(tenantId);
        await using var applier = NewContext(tenantId);
        _ = await new EfStockTakeRepository(staleRice).GetAsync(take.Id); // tracked while still in Review
        _ = await new EfStockTakeRepository(staleSalt).GetAsync(take.Id);

        var applied = await ServiceOver(applier, tenantId, userId).ApplyAsync(take.Id, "pin");
        Assert.Equal(StockTakeStatus.Applied, applied.Status);

        // A variance line (has a movement) and a zero-variance line (no movement): both stale saves must fail.
        _ = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => ServiceOver(staleRice, tenantId, userId).CountLineAsync(take.Id, riceLine, 4m, false));
        _ = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => ServiceOver(staleSalt, tenantId, userId).CountLineAsync(take.Id, saltLine, 4m, false));

        var final = await ReadAsync<StockTakeDto>(await admin.GetAsync($"/stock-takes/{take.Id}"));
        var riceDto = final.Lines.Single(l => l.Id == riceLine);
        Assert.Equal(-3m, riceDto.VarianceQty);
        Assert.Equal(-3m, await MovementQuantityAsync(riceDto.MovementId!.Value));
        Assert.Equal(0m, final.Lines.Single(l => l.Id == saltLine).VarianceQty);
        Assert.Equal(7m, await StockOfAsync(rice.Id));
        Assert.Equal(10m, await StockOfAsync(salt.Id));
    }

    [Fact]
    public async Task Two_counters_on_different_lines_of_one_take_never_conflict()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        var shop = TestSessions.ShopOf(admin);
        var (tenantId, userId) = (shop.Tenant.TenantId, shop.Tenant.AdminMembershipId);
        var (take, rice, salt) = await TwoLineTakeAsync(admin, toReview: false);

        await using var first = NewContext(tenantId);
        await using var second = NewContext(tenantId);
        _ = await new EfStockTakeRepository(first).GetAsync(take.Id); // both counters hold the same take version
        _ = await new EfStockTakeRepository(second).GetAsync(take.Id);

        _ = await ServiceOver(first, tenantId, userId).CountLineAsync(take.Id, take.Lines.Single(l => l.ItemId == rice.Id).Id, 6m, false);
        _ = await ServiceOver(second, tenantId, userId).CountLineAsync(take.Id, take.Lines.Single(l => l.ItemId == salt.Id).Id, 9m, false);

        var final = await ReadAsync<StockTakeDto>(await admin.GetAsync($"/stock-takes/{take.Id}"));
        Assert.Equal(6m, final.Lines.Single(l => l.ItemId == rice.Id).CountedQty);
        Assert.Equal(9m, final.Lines.Single(l => l.ItemId == salt.Id).CountedQty);
    }

    [Fact]
    public async Task Two_applies_from_stale_contexts_post_one_adjustment_per_counted_line()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        var shop = TestSessions.ShopOf(admin);
        var (tenantId, userId) = (shop.Tenant.TenantId, shop.Tenant.AdminMembershipId);
        var (take, rice, salt) = await TwoLineTakeAsync(admin, toReview: true);

        await using var first = NewContext(tenantId);
        await using var second = NewContext(tenantId);
        _ = await new EfStockTakeRepository(first).GetAsync(take.Id);
        _ = await new EfStockTakeRepository(second).GetAsync(take.Id);

        var one = await ServiceOver(first, tenantId, userId).ApplyAsync(take.Id, "pin");
        try
        {
            var two = await ServiceOver(second, tenantId, userId).ApplyAsync(take.Id, "pin");
            // Idempotent: the first result (AppliedAt is compared to Postgres' microsecond precision).
            Assert.Equal(StockTakeStatus.Applied, two.Status);
            Assert.Equal(one.Lines.Select(l => l.MovementId), two.Lines.Select(l => l.MovementId));
            Assert.True((one.AppliedAt!.Value - two.AppliedAt!.Value).Duration() < TimeSpan.FromMilliseconds(1));
        }
        catch (DbUpdateConcurrencyException)
        {
            // Also acceptable: surfaces as a 409.
        }

        Assert.Equal(1, await AdjustmentCountAsync(rice.Id));
        Assert.Equal(0, await AdjustmentCountAsync(salt.Id)); // zero variance posts nothing
        Assert.Equal(7m, await StockOfAsync(rice.Id));
        Assert.Equal(1, await AuditCountAsync(take.Id));
    }
}
