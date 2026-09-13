using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Purch.Application.Auth;
using Purch.Application.Onboarding;
using Purch.Application.Sync;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class SyncEndpointsTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Syncing_a_new_item_records_it_as_applied()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var entityId = Guid.NewGuid();

        var response = await client.PostAsJsonAsync(
            "/sync",
            new SyncBatchRequest([
                new SyncItemRequest(
                    $"key-{Guid.NewGuid():N}",
                    "Transaction",
                    entityId,
                    DateTimeOffset.UtcNow,
                    "{}"),
            ]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SyncBatchResultDto>(JsonOptions);
        Assert.Equal(SyncItemStatus.Applied, result!.Results.Single().Status);
    }

    [Fact]
    public async Task Replaying_the_same_idempotency_key_is_a_safe_no_op()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var entityId = Guid.NewGuid();
        var idempotencyKey = $"key-{Guid.NewGuid():N}";

        var item = new SyncItemRequest(idempotencyKey, "Transaction", entityId, DateTimeOffset.UtcNow, "{}");
        _ = await client.PostAsJsonAsync("/sync", new SyncBatchRequest([item]));

        var replayResponse = await client.PostAsJsonAsync("/sync", new SyncBatchRequest([item]));
        var replayResult = await replayResponse.Content.ReadFromJsonAsync<SyncBatchResultDto>(JsonOptions);

        Assert.Equal(SyncItemStatus.AlreadySynced, replayResult!.Results.Single().Status);
    }

    [Fact]
    public async Task A_later_timestamped_item_from_a_different_device_is_flagged_not_applied()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var deviceA = await AuthenticatedAdminClientAsync(factory);
        using var deviceB = await PairSecondDeviceAsync(factory, deviceA);
        var entityId = Guid.NewGuid();
        var earlier = DateTimeOffset.UtcNow;
        var later = earlier.AddMinutes(5);

        var firstResponse = await deviceA.PostAsJsonAsync(
            "/sync",
            new SyncBatchRequest([new SyncItemRequest($"key-{Guid.NewGuid():N}", "Item", entityId, earlier, "{}")]));
        var firstResult = await firstResponse.Content.ReadFromJsonAsync<SyncBatchResultDto>(JsonOptions);
        Assert.Equal(SyncItemStatus.Applied, firstResult!.Results.Single().Status);

        var secondResponse = await deviceB.PostAsJsonAsync(
            "/sync",
            new SyncBatchRequest([new SyncItemRequest($"key-{Guid.NewGuid():N}", "Item", entityId, later, "{}")]));
        var secondResult = await secondResponse.Content.ReadFromJsonAsync<SyncBatchResultDto>(JsonOptions);

        Assert.Equal(SyncItemStatus.ConflictFlagged, secondResult!.Results.Single().Status);

        var flagged = await deviceA.GetFromJsonAsync<List<FlaggedSyncRecordDto>>("/sync/flagged", JsonOptions);
        _ = Assert.Single(flagged!);
    }

    [Fact]
    public async Task An_out_of_order_earlier_item_wins_and_retroactively_flags_the_previous_winner()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var deviceA = await AuthenticatedAdminClientAsync(factory);
        using var deviceB = await PairSecondDeviceAsync(factory, deviceA);
        var entityId = Guid.NewGuid();
        var earlier = DateTimeOffset.UtcNow;
        var later = earlier.AddMinutes(5);

        // Device A's later-timestamped change arrives first (e.g. it reconnected sooner).
        _ = await deviceA.PostAsJsonAsync(
            "/sync",
            new SyncBatchRequest([new SyncItemRequest($"key-{Guid.NewGuid():N}", "Item", entityId, later, "{}")]));

        // Device B's earlier-timestamped change arrives after — it should win instead.
        var response = await deviceB.PostAsJsonAsync(
            "/sync",
            new SyncBatchRequest([new SyncItemRequest($"key-{Guid.NewGuid():N}", "Item", entityId, earlier, "{}")]));
        var result = await response.Content.ReadFromJsonAsync<SyncBatchResultDto>(JsonOptions);

        Assert.Equal(SyncItemStatus.Applied, result!.Results.Single().Status);

        var flagged = await deviceA.GetFromJsonAsync<List<FlaggedSyncRecordDto>>("/sync/flagged", JsonOptions);
        var flaggedRecord = Assert.Single(flagged!);
        // Postgres timestamptz only keeps microsecond precision, one digit less
        // than a .NET tick — a round trip through the database loses that last
        // digit, so compare with a tolerance rather than exact equality.
        Assert.Equal(later, flaggedRecord.ClientTimestamp, TimeSpan.FromMicroseconds(1));
    }

    [Fact]
    public async Task Acknowledging_a_flagged_record_sets_its_reviewed_at()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var deviceA = await AuthenticatedAdminClientAsync(factory);
        using var deviceB = await PairSecondDeviceAsync(factory, deviceA);
        var entityId = Guid.NewGuid();
        var earlier = DateTimeOffset.UtcNow;
        var later = earlier.AddMinutes(5);

        _ = await deviceA.PostAsJsonAsync(
            "/sync",
            new SyncBatchRequest([new SyncItemRequest($"key-{Guid.NewGuid():N}", "Item", entityId, earlier, "{}")]));
        _ = await deviceB.PostAsJsonAsync(
            "/sync",
            new SyncBatchRequest([new SyncItemRequest($"key-{Guid.NewGuid():N}", "Item", entityId, later, "{}")]));

        var flagged = await deviceA.GetFromJsonAsync<List<FlaggedSyncRecordDto>>("/sync/flagged", JsonOptions);
        var flaggedRecord = Assert.Single(flagged!);

        var response = await deviceA.PostAsync($"/sync/flagged/{flaggedRecord.Id}/acknowledge", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var acknowledged = await response.Content.ReadFromJsonAsync<FlaggedSyncRecordDto>(JsonOptions);
        _ = Assert.NotNull(acknowledged!.ReviewedAt);
    }

    private static async Task<HttpClient> PairSecondDeviceAsync(PurchApiFactory factory, HttpClient existingAdminClient)
    {
        var branches = await existingAdminClient.GetFromJsonAsync<List<BranchDto>>("/branches", JsonOptions);
        var branchId = branches!.Single().Id;

        var deviceResponse = await existingAdminClient.PostAsJsonAsync(
            "/devices",
            new CreateDeviceRequest(branchId, "Second Terminal"));
        var device = await deviceResponse.Content.ReadFromJsonAsync<DeviceDto>(JsonOptions);

        var client = factory.CreateClient();
        var loginResponse = await client.PostAsJsonAsync(
            "/auth/login",
            new LoginRequest(device!.PairingCode, "1234"));
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<LoginResponseBody>(JsonOptions);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginBody!.AccessToken);
        return client;
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
