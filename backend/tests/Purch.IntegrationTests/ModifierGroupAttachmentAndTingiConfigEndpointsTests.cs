using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Purch.Application.Auth;
using Purch.Application.Catalog;
using Purch.Application.Onboarding;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class ModifierGroupAttachmentAndTingiConfigEndpointsTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Admin_can_attach_a_modifier_group_to_an_item_and_it_round_trips_on_list()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Iced Tea", null, null, null, 35m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        var groupResponse = await client.PostAsJsonAsync(
            "/modifier-groups",
            new CreateModifierGroupRequest("Ice Level", false));
        var group = await groupResponse.Content.ReadFromJsonAsync<ModifierGroupDto>(JsonOptions);

        var attachResponse = await client.PostAsJsonAsync(
            $"/items/{item!.Id}/modifier-groups",
            new AttachModifierGroupRequest(group!.Id));

        Assert.Equal(HttpStatusCode.OK, attachResponse.StatusCode);

        var listResponse = await client.GetAsync($"/items/{item.Id}/modifier-groups");
        var groups = await listResponse.Content.ReadFromJsonAsync<List<ModifierGroupDto>>(JsonOptions);

        _ = Assert.Single(groups!);
        Assert.Equal("Ice Level", groups![0].Name);
    }

    [Fact]
    public async Task Attaching_the_same_modifier_group_twice_is_rejected_as_a_conflict()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Cheeseburger", null, null, null, 89m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        var groupResponse = await client.PostAsJsonAsync(
            "/modifier-groups",
            new CreateModifierGroupRequest("No Pickles", false));
        var group = await groupResponse.Content.ReadFromJsonAsync<ModifierGroupDto>(JsonOptions);

        _ = await client.PostAsJsonAsync($"/items/{item!.Id}/modifier-groups", new AttachModifierGroupRequest(group!.Id));
        var secondAttachResponse = await client.PostAsJsonAsync(
            $"/items/{item.Id}/modifier-groups",
            new AttachModifierGroupRequest(group.Id));

        Assert.Equal(HttpStatusCode.Conflict, secondAttachResponse.StatusCode);
    }

    [Fact]
    public async Task Admin_can_set_fixed_tingi_sizes_on_a_weight_volume_item()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Rice (Sack)", null, null, null, 60m, null, PricingType.WeightVolume));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        var configResponse = await client.PutAsJsonAsync(
            $"/items/{item!.Id}/tingi-config",
            new UpdateTingiConfigRequest(TingiMode.FixedSizes, 1m, null, [0.1m, 0.5m]));

        Assert.Equal(HttpStatusCode.OK, configResponse.StatusCode);
        var updated = await configResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);
        Assert.Equal(TingiMode.FixedSizes, updated!.TingiMode);
        Assert.Equal(2, updated.TingiAllowedSizes.Count);
        Assert.Contains(0.1m, updated.TingiAllowedSizes);
    }

    [Fact]
    public async Task Admin_can_set_a_tingi_increment_step_on_a_weight_volume_item()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Manure", null, null, null, 5m, null, PricingType.WeightVolume));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        var configResponse = await client.PutAsJsonAsync(
            $"/items/{item!.Id}/tingi-config",
            new UpdateTingiConfigRequest(TingiMode.Increment, 50m, 10m, null));

        Assert.Equal(HttpStatusCode.OK, configResponse.StatusCode);
        var updated = await configResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);
        Assert.Equal(TingiMode.Increment, updated!.TingiMode);
        Assert.Equal(10m, updated.TingiIncrementStep);
        Assert.Equal(50m, updated.PackagedSize);
    }

    [Fact]
    public async Task Setting_tingi_config_on_a_unit_priced_item_is_rejected()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Canned Sardines", null, null, null, 25m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        var configResponse = await client.PutAsJsonAsync(
            $"/items/{item!.Id}/tingi-config",
            new UpdateTingiConfigRequest(TingiMode.FixedSizes, 1m, null, [0.5m]));

        Assert.Equal(HttpStatusCode.BadRequest, configResponse.StatusCode);
    }

    [Fact]
    public async Task A_fixed_tingi_size_larger_than_the_pack_size_is_rejected()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Fertilizer", null, null, null, 10m, null, PricingType.WeightVolume));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        var configResponse = await client.PutAsJsonAsync(
            $"/items/{item!.Id}/tingi-config",
            new UpdateTingiConfigRequest(TingiMode.FixedSizes, 25m, null, [30m]));

        Assert.Equal(HttpStatusCode.BadRequest, configResponse.StatusCode);
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
