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
public sealed class BundleAndVariantEndpointsTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Admin_can_add_a_bundle_rule_to_a_bundle_priced_item()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Instant Noodles", null, null, null, 15m, null, PricingType.Bundle));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        var ruleResponse = await client.PostAsJsonAsync(
            $"/items/{item!.Id}/bundle-rules",
            new CreateBundlePromoRuleRequest("Buy 2 Get 1", 3, 30m));

        Assert.Equal(HttpStatusCode.OK, ruleResponse.StatusCode);
        var rule = await ruleResponse.Content.ReadFromJsonAsync<BundlePromoRuleDto>(JsonOptions);
        Assert.Equal(3, rule!.TriggerQuantity);
    }

    [Fact]
    public async Task Adding_a_bundle_rule_to_a_unit_priced_item_is_rejected()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Canned Tuna", null, null, null, 40m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        var ruleResponse = await client.PostAsJsonAsync(
            $"/items/{item!.Id}/bundle-rules",
            new CreateBundlePromoRuleRequest("3 for ₱99", 3, 99m));

        Assert.Equal(HttpStatusCode.BadRequest, ruleResponse.StatusCode);
    }

    [Fact]
    public async Task A_bundle_rule_with_fewer_than_two_trigger_units_is_rejected()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Soap Bar", null, null, null, 20m, null, PricingType.Bundle));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        var ruleResponse = await client.PostAsJsonAsync(
            $"/items/{item!.Id}/bundle-rules",
            new CreateBundlePromoRuleRequest("Nonsense", 1, 20m));

        Assert.Equal(HttpStatusCode.BadRequest, ruleResponse.StatusCode);
    }

    [Fact]
    public async Task Admin_can_add_a_variant_to_a_variant_matrix_item_and_attributes_round_trip()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("T-Shirt", null, null, null, 250m, null, PricingType.VariantMatrix));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        var variantResponse = await client.PostAsJsonAsync(
            $"/items/{item!.Id}/variants",
            new CreateItemVariantRequest(
                new Dictionary<string, string> { ["size"] = "M", ["color"] = "Blue" },
                "TSHIRT-M-BLUE",
                299m,
                null));

        Assert.Equal(HttpStatusCode.OK, variantResponse.StatusCode);
        var variant = await variantResponse.Content.ReadFromJsonAsync<ItemVariantDto>(JsonOptions);
        Assert.Equal("M", variant!.Attributes["size"]);
        Assert.Equal("Blue", variant.Attributes["color"]);
        Assert.Equal(299m, variant.PriceOverride);
    }

    [Fact]
    public async Task Adding_a_variant_with_no_attributes_is_rejected()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Shoes", null, null, null, 800m, null, PricingType.VariantMatrix));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        var variantResponse = await client.PostAsJsonAsync(
            $"/items/{item!.Id}/variants",
            new CreateItemVariantRequest(new Dictionary<string, string>(), null, null, null));

        Assert.Equal(HttpStatusCode.BadRequest, variantResponse.StatusCode);
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
