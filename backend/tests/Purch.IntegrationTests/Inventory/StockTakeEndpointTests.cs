using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
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

    [Fact]
    public async Task Concurrent_applies_post_the_variance_exactly_once()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        var item = await StockedItemAsync(admin, "Rice", 10m);
        var take = await ReviewTakeWithCountAsync(admin, item, 7m);

        var responses = await Task.WhenAll(
            admin.PostAsJsonAsync($"/stock-takes/{take.Id}/apply", new ApplyStockTakeRequest(TestSessions.AdminPin)),
            admin.PostAsJsonAsync($"/stock-takes/{take.Id}/apply", new ApplyStockTakeRequest(TestSessions.AdminPin)));

        Assert.All(responses, r => Assert.True(r.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict, r.StatusCode.ToString()));
        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Equal(7m, await StockOfAsync(item.Id));
        Assert.Equal(1, await AuditCountAsync(take.Id));
    }

    [Fact]
    public async Task A_count_racing_apply_never_leaves_the_line_and_its_movement_disagreeing()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        var item = await StockedItemAsync(admin, "Rice", 10m);
        var take = await ReviewTakeWithCountAsync(admin, item, 7m);
        var lineId = take.Lines.Single().Id;

        _ = await Task.WhenAll(
            admin.PutAsJsonAsync($"/stock-takes/{take.Id}/lines/{lineId}", new CountStockTakeLineRequest(4m)),
            admin.PostAsJsonAsync($"/stock-takes/{take.Id}/apply", new ApplyStockTakeRequest(TestSessions.AdminPin)));

        var final = await ReadAsync<StockTakeDto>(await admin.GetAsync($"/stock-takes/{take.Id}"));
        var line = final.Lines.Single();
        if (final.Status == StockTakeStatus.Applied)
        {
            Assert.Equal(line.VarianceQty, await MovementQuantityAsync(line.MovementId!.Value));
            Assert.Equal(10m + line.VarianceQty, await StockOfAsync(item.Id));
        }
        else
        {
            Assert.Null(line.MovementId);
            Assert.Equal(10m, await StockOfAsync(item.Id));
        }
    }
}
