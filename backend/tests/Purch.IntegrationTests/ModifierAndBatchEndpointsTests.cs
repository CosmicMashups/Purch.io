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
public sealed class ModifierAndBatchEndpointsTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Admin_can_create_a_modifier_group_and_add_a_modifier_to_it()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var groupResponse = await client.PostAsJsonAsync(
            "/modifier-groups",
            new CreateModifierGroupRequest("Add-ons", true, false));
        Assert.Equal(HttpStatusCode.OK, groupResponse.StatusCode);
        var group = await groupResponse.Content.ReadFromJsonAsync<ModifierGroupDto>(JsonOptions);
        Assert.Empty(group!.Modifiers);

        var modifierResponse = await client.PostAsJsonAsync(
            $"/modifier-groups/{group.Id}/modifiers",
            new CreateItemModifierRequest("Extra Cheese", 15m));
        Assert.Equal(HttpStatusCode.OK, modifierResponse.StatusCode);
        var updatedGroup = await modifierResponse.Content.ReadFromJsonAsync<ModifierGroupDto>(JsonOptions);

        _ = Assert.Single(updatedGroup!.Modifiers);
        Assert.Equal("Extra Cheese", updatedGroup.Modifiers[0].Name);
    }

    [Fact]
    public async Task A_required_modifier_group_round_trips_through_creation_and_item_attachment()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var groupResponse = await client.PostAsJsonAsync(
            "/modifier-groups",
            new CreateModifierGroupRequest("Sugar Level", false, true));
        var group = await groupResponse.Content.ReadFromJsonAsync<ModifierGroupDto>(JsonOptions);
        Assert.True(group!.IsRequired);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Iced Coffee", null, null, null, 89m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        var attachResponse = await client.PostAsJsonAsync(
            $"/items/{item!.Id}/modifier-groups",
            new AttachModifierGroupRequest(group.Id));
        var attached = await attachResponse.Content.ReadFromJsonAsync<ModifierGroupDto>(JsonOptions);

        Assert.True(attached!.IsRequired);
    }

    [Fact]
    public async Task Receiving_a_batch_against_a_weight_volume_item_increases_its_stock_on_hand()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Rice", null, null, null, 55m, null, PricingType.WeightVolume));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        var batchResponse = await client.PostAsJsonAsync(
            $"/items/{item!.Id}/batches",
            new CreateItemBatchRequest("LOT-001", null, 50m));
        Assert.Equal(HttpStatusCode.OK, batchResponse.StatusCode);
        var batch = await batchResponse.Content.ReadFromJsonAsync<ItemBatchDto>(JsonOptions);
        Assert.Equal(50m, batch!.QuantityRemaining);

        var itemsAfter = await client.GetFromJsonAsync<List<ItemDto>>("/items", JsonOptions);
        var updatedItem = itemsAfter!.Single(i => i.Id == item.Id);
        Assert.Equal(50m, updatedItem.StockOnHand);
    }

    [Fact]
    public async Task Recording_a_batch_against_a_unit_priced_item_is_rejected()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Canned Goods", null, null, null, 25m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        var batchResponse = await client.PostAsJsonAsync(
            $"/items/{item!.Id}/batches",
            new CreateItemBatchRequest("LOT-002", null, 10m));

        Assert.Equal(HttpStatusCode.BadRequest, batchResponse.StatusCode);
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
