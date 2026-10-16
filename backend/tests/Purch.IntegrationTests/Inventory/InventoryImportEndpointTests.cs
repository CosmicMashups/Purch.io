using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Npgsql;
using Purch.Application.Catalog;
using Purch.Application.Inventory;
using Purch.Application.Inventory.Import;
using Purch.Common.TestUtilities;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests.Inventory;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class InventoryImportEndpointTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string CatalogHeader = "type,sku,barcode,name,category,price,cost,reorder_threshold,unit,active,opening_stock";

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }

    private static Task<HttpResponseMessage> UploadAsync(HttpClient client, string csv, string kind, string? key = null) =>
        UploadBytesAsync(client, Encoding.UTF8.GetBytes(csv), kind, key);

    private static async Task<HttpResponseMessage> UploadBytesAsync(HttpClient client, byte[] bytes, string kind, string? key = null)
    {
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(file, "file", "import.csv");
        content.Add(new StringContent(kind), "kind");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/inventory/imports") { Content = content };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString());
        return await client.SendAsync(request);
    }

    private async Task<T?> ScalarAsync<T>(string sql, Guid id)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        _ = command.Parameters.AddWithValue("id", id);
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? default : (T)value;
    }

    [Fact]
    public async Task A_cashier_is_refused_every_import_route()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        using var cashier = await TestSessions.CashierClientAsync(admin);
        var id = Guid.NewGuid();

        foreach (var response in new[]
        {
            await UploadAsync(cashier, CatalogHeader + "\nitem,N1,,Noodles,,5,,,,,", "catalog"),
            await cashier.GetAsync($"/inventory/imports/{id}"),
            await cashier.PutAsJsonAsync($"/inventory/imports/{id}/resolutions", new Dictionary<string, ImportResolutionDto>()),
            await cashier.PostAsJsonAsync($"/inventory/imports/{id}/commit", new CommitImportRequest(TestSessions.AdminPin, "x", true)),
        })
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public async Task Upload_review_resolve_and_commit_creates_updates_and_posts_stock()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        var cola = await ReadAsync<ItemDto>(await admin.PostAsJsonAsync("/items", new CreateItemRequest("Cola", "C1", null, null, 10m, null, PricingType.Unit)));
        var rice = await ReadAsync<ItemDto>(await admin.PostAsJsonAsync("/items", new CreateItemRequest("Basmati Rice", "R1", null, null, 50m, null, PricingType.Unit)));
        var branchId = TestSessions.ShopOf(admin).Tenant.BranchId;
        _ = await ReadAsync<InventoryMovementDto>(await admin.PostAsJsonAsync(
            "/inventory/movements", new RecordMovementRequest(rice.Id, branchId, MovementType.StockIn, 10m, null, null, null, null)));

        var csv = CatalogHeader + "\nitem,N1,,Noodles,Pantry,5,2,,,,7\nitem,C1,,Cola,,12,,,,,\nitem,C2,,Colla,,6,,,,,";
        var key = Guid.NewGuid().ToString();
        var uploaded = await ReadAsync<ImportBatchDto>(await UploadAsync(admin, csv, "catalog", key));
        Assert.Equal(uploaded.Id, (await ReadAsync<ImportBatchDto>(await UploadAsync(admin, csv, "catalog", key))).Id);
        Assert.True(uploaded.Blocked);
        var decision = Assert.Single(uploaded.Plan.NeedsDecision);

        var reviewed = await ReadAsync<ImportBatchDto>(await admin.GetAsync($"/inventory/imports/{uploaded.Id}"));
        Assert.Equal(uploaded.PlanHash, reviewed.PlanHash);
        var resolved = await ReadAsync<ImportBatchDto>(await admin.PutAsJsonAsync(
            $"/inventory/imports/{uploaded.Id}/resolutions",
            new Dictionary<string, ImportResolutionDto> { [decision.Key] = new("rename", NewName: "Lemonade") }));
        Assert.False(resolved.Blocked);
        Assert.Equal("rename", resolved.Resolutions[decision.Key].Choice);

        var committed = await ReadAsync<ImportCommitResultDto>(await admin.PostAsJsonAsync(
            $"/inventory/imports/{uploaded.Id}/commit", new CommitImportRequest(TestSessions.AdminPin, resolved.PlanHash, true)));
        Assert.True(committed.Committed);
        Assert.Contains(committed.Result!.CreatedCategories, c => c.Name == "Pantry");
        var noodles = committed.Result.CreatedRecords.Single(r => r.Name == "Noodles");
        Assert.Contains(committed.Result.CreatedRecords, r => r.Name == "Lemonade");
        Assert.Equal(1, committed.Result.UpdatedCount);

        Assert.Equal(7m, await ScalarAsync<decimal>("SELECT \"StockOnHand\" FROM \"Items\" WHERE \"Id\" = @id", noodles.Id));
        Assert.Equal(2m, await ScalarAsync<decimal?>("SELECT \"CostPrice\" FROM \"Items\" WHERE \"Id\" = @id", noodles.Id));
        Assert.Equal(committed.Result.CreatedCategories.Single().Id, await ScalarAsync<Guid>("SELECT \"CategoryId\" FROM \"Items\" WHERE \"Id\" = @id", noodles.Id));
        Assert.Equal(12m, await ScalarAsync<decimal>("SELECT \"BasePrice\" FROM \"Items\" WHERE \"Id\" = @id", cola.Id));
        Assert.Equal(1L, await ScalarAsync<long>(
            $"SELECT COUNT(*) FROM \"AuditLogs\" WHERE \"TargetEntityId\" = @id AND \"ActionType\" = {(int)AuditActionType.InventoryImportCommitted} AND \"ApprovedByUserId\" IS NOT NULL", uploaded.Id));

        // A repeated commit replays the stored result and applies nothing.
        var replay = await ReadAsync<ImportCommitResultDto>(await admin.PostAsJsonAsync(
            $"/inventory/imports/{uploaded.Id}/commit", new CommitImportRequest(null, null, false)));
        Assert.Equal(noodles.Id, replay.Result!.CreatedRecords.Single(r => r.Name == "Noodles").Id);
        Assert.Equal(7m, await ScalarAsync<decimal>("SELECT \"StockOnHand\" FROM \"Items\" WHERE \"Id\" = @id", noodles.Id));

        // Stock import: counted 8 against 10 on hand posts a -2 Adjustment tagged with the batch.
        var stock = await ReadAsync<ImportBatchDto>(await UploadAsync(admin, "type,sku,barcode,name,counted_qty,note\nitem,R1,,Basmati Rice,8,shelf", "stock"));
        var stockCommit = await ReadAsync<ImportCommitResultDto>(await admin.PostAsJsonAsync(
            $"/inventory/imports/{stock.Id}/commit", new CommitImportRequest(TestSessions.AdminPin, stock.PlanHash, false)));
        var movement = Assert.Single(stockCommit.Result!.Movements);
        Assert.Equal(MovementType.Adjustment, movement.Type);
        Assert.Equal(-2m, movement.Quantity);
        Assert.Equal(8m, await ScalarAsync<decimal>("SELECT \"StockOnHand\" FROM \"Items\" WHERE \"Id\" = @id", rice.Id));
        Assert.Equal($"import:{stock.Id} shelf", await ScalarAsync<string>("SELECT \"Note\" FROM \"InventoryMovements\" WHERE \"Id\" = @id", movement.MovementId));
    }

    [Fact]
    public async Task A_stale_plan_hash_is_a_conflict_that_applies_nothing()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        var uploaded = await ReadAsync<ImportBatchDto>(await UploadAsync(admin, CatalogHeader + "\nitem,N1,,Noodles,,5,,,,,", "catalog"));

        var response = await admin.PostAsJsonAsync($"/inventory/imports/{uploaded.Id}/commit", new CommitImportRequest(TestSessions.AdminPin, "stale", true));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ImportCommitResultDto>(JsonOptions))!;
        Assert.False(body.Committed);
        Assert.Equal(ImportBatchStatus.Previewed, body.Batch!.Status);
        Assert.Equal(0L, await ScalarAsync<long>("SELECT COUNT(*) FROM \"Items\" WHERE \"TenantId\" = @id", TestSessions.ShopOf(admin).Tenant.TenantId));
    }

    [Fact]
    public async Task An_oversized_body_is_rejected()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        var big = new byte[CsvImportParser.MaxBytes + (256 * 1024)];
        Array.Fill(big, (byte)'a');

        var response = await UploadBytesAsync(admin, big, "catalog");

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }
    private Task<long> CountAsync(string table, Guid tenantId) =>
        ScalarAsync<long>($"SELECT COUNT(*) FROM \"{table}\" WHERE \"TenantId\" = @id", tenantId);

    [Fact]
    public async Task A_catalog_change_after_review_is_a_conflict_that_keeps_the_batch_and_its_decisions()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        var tenantId = TestSessions.ShopOf(admin).Tenant.TenantId;
        var cola = await ReadAsync<ItemDto>(await admin.PostAsJsonAsync("/items", new CreateItemRequest("Cola", "C1", null, null, 10m, null, PricingType.Unit)));
        var uploaded = await ReadAsync<ImportBatchDto>(await UploadAsync(admin, CatalogHeader + "\nitem,C1,,Cola,,12,,,,,\nitem,C2,,Colla,,6,,,,,", "catalog"));
        var key = Assert.Single(uploaded.Plan.NeedsDecision).Key;
        var resolved = await ReadAsync<ImportBatchDto>(await admin.PutAsJsonAsync($"/inventory/imports/{uploaded.Id}/resolutions",
            new Dictionary<string, ImportResolutionDto> { [key] = new("create") }));
        _ = await ReadAsync<ImportBatchDto>(await admin.GetAsync($"/inventory/imports/{uploaded.Id}"));
        _ = await ReadAsync<ItemDto>(await admin.PutAsJsonAsync($"/items/{cola.Id}", new UpdateItemRequest("Cola", "C1", null, null, 11m, null, true)));

        var response = await admin.PostAsJsonAsync($"/inventory/imports/{uploaded.Id}/commit", new CommitImportRequest(TestSessions.AdminPin, resolved.PlanHash, true));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ImportCommitResultDto>(JsonOptions))!;
        Assert.Contains(2, body.ChangedLines);
        Assert.Equal(11m, await ScalarAsync<decimal>("SELECT \"BasePrice\" FROM \"Items\" WHERE \"Id\" = @id", cola.Id));
        Assert.Equal(1L, await CountAsync("Items", tenantId));
        var reloaded = await ReadAsync<ImportBatchDto>(await admin.GetAsync($"/inventory/imports/{uploaded.Id}"));
        Assert.Equal(ImportBatchStatus.Previewed, reloaded.Status);
        Assert.Equal("create", reloaded.Resolutions[key].Choice);
    }

    [Fact]
    public async Task A_wrong_pin_persists_the_failed_attempt_and_applies_nothing()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        var shop = TestSessions.ShopOf(admin);
        var uploaded = await ReadAsync<ImportBatchDto>(await UploadAsync(admin, CatalogHeader + "\nitem,N1,,Noodles,Pantry,5,,,,,4", "catalog"));
        const string Attempts = "SELECT \"ApproverPinFailedAttempts\" FROM \"Devices\" WHERE \"Id\" = @id";
        var before = await ScalarAsync<int>(Attempts, shop.RegisterId);

        var response = await admin.PostAsJsonAsync($"/inventory/imports/{uploaded.Id}/commit", new CommitImportRequest("000000", uploaded.PlanHash, true));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before + 1, await ScalarAsync<int>(Attempts, shop.RegisterId));
        Assert.Equal(0L, await CountAsync("Items", shop.Tenant.TenantId));
        Assert.Equal(0L, await CountAsync("Categories", shop.Tenant.TenantId));
        Assert.Equal(0L, await CountAsync("InventoryMovements", shop.Tenant.TenantId));
    }

    [Fact]
    public async Task Two_parallel_commits_apply_once()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        var tenantId = TestSessions.ShopOf(admin).Tenant.TenantId;
        var uploaded = await ReadAsync<ImportBatchDto>(await UploadAsync(admin, CatalogHeader + "\nitem,N1,,Noodles,Pantry,5,,,,,4", "catalog"));
        var commit = new CommitImportRequest(TestSessions.AdminPin, uploaded.PlanHash, true);

        var responses = await Task.WhenAll(
            admin.PostAsJsonAsync($"/inventory/imports/{uploaded.Id}/commit", commit),
            admin.PostAsJsonAsync($"/inventory/imports/{uploaded.Id}/commit", commit));

        Assert.All(responses, r => Assert.True(r.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict, r.StatusCode.ToString()));
        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Equal(1L, await CountAsync("Items", tenantId));
        Assert.Equal(1L, await CountAsync("Categories", tenantId));
        Assert.Equal(1L, await CountAsync("InventoryMovements", tenantId));
    }

    [Fact]
    public async Task Warehouse_uploads_and_resolves_and_commits_with_a_manager_pin()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        using var manager = await TestSessions.ManagerClientAsync(admin);
        using var warehouse = await TestSessions.StaffClientAsync(admin, "Wes Warehouse", MembershipRole.Staff, StaffDuty.Warehouse, "456712");
        _ = await ReadAsync<ItemDto>(await admin.PostAsJsonAsync("/items", new CreateItemRequest("Cola", "C1", null, null, 10m, null, PricingType.Unit)));

        var uploaded = await ReadAsync<ImportBatchDto>(await UploadAsync(warehouse, CatalogHeader + "\nitem,C2,,Colla,,6,,,,,", "catalog"));
        var resolved = await ReadAsync<ImportBatchDto>(await warehouse.PutAsJsonAsync($"/inventory/imports/{uploaded.Id}/resolutions",
            new Dictionary<string, ImportResolutionDto> { [uploaded.Plan.NeedsDecision.Single().Key] = new("create") }));
        Assert.Equal(HttpStatusCode.BadRequest, (await warehouse.PostAsJsonAsync($"/inventory/imports/{uploaded.Id}/commit", new CommitImportRequest("456712", resolved.PlanHash, true))).StatusCode);

        var committed = await ReadAsync<ImportCommitResultDto>(await warehouse.PostAsJsonAsync(
            $"/inventory/imports/{uploaded.Id}/commit", new CommitImportRequest("567812", resolved.PlanHash, true)));

        Assert.True(committed.Committed);
        Assert.Equal("Colla", committed.Result!.CreatedRecords.Single().Name);
    }

    [Fact]
    public async Task A_failure_on_a_later_row_rolls_back_categories_and_items_created_earlier()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        var tenantId = TestSessions.ShopOf(admin).Tenant.TenantId;
        // A deleted item keeps its barcode: the planner (non-deleted records only) plans a create, ItemService refuses it.
        var old = await ReadAsync<ItemDto>(await admin.PostAsJsonAsync("/items", new CreateItemRequest("Old Soda", "OLD", "4800001", null, 10m, null, PricingType.Unit)));
        await using (var connection = new NpgsqlConnection(postgres.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("UPDATE \"Items\" SET \"IsDeleted\" = TRUE WHERE \"Id\" = @id", connection);
            _ = command.Parameters.AddWithValue("id", old.Id);
            _ = await command.ExecuteNonQueryAsync();
        }

        var uploaded = await ReadAsync<ImportBatchDto>(await UploadAsync(admin,
            CatalogHeader + "\nitem,N1,,Noodles,Pantry,5,,,,,4\nitem,S2,4800001,Fizz,Pantry,5,,,,,", "catalog"));
        Assert.False(uploaded.Blocked);

        var response = await admin.PostAsJsonAsync($"/inventory/imports/{uploaded.Id}/commit", new CommitImportRequest(TestSessions.AdminPin, uploaded.PlanHash, true));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(0L, await CountAsync("Categories", tenantId));
        Assert.Equal(1L, await CountAsync("Items", tenantId));
        Assert.Equal(0L, await CountAsync("InventoryMovements", tenantId));
        Assert.Equal(ImportBatchStatus.Previewed, (await ReadAsync<ImportBatchDto>(await admin.GetAsync($"/inventory/imports/{uploaded.Id}"))).Status);
    }
}
