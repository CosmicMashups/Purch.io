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
public sealed class CatalogConditionalGetTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Items_list_returns_an_etag_and_answers_304_when_nothing_changed()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        _ = await client.PostAsJsonAsync("/items", new CreateItemRequest("Water", null, null, null, 15m, null, PricingType.Unit));

        var first = await client.GetAsync("/items");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var etag = first.Headers.ETag;
        Assert.NotNull(etag);
        Assert.Contains("no-cache", first.Headers.CacheControl!.ToString());

        var revalidate = await SendWithIfNoneMatchAsync(client, "/items", etag!.ToString());

        Assert.Equal(HttpStatusCode.NotModified, revalidate.StatusCode);
        Assert.Equal(etag.ToString(), revalidate.Headers.ETag!.ToString());
        Assert.Empty(await revalidate.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Editing_an_item_changes_the_etag_so_the_client_refetches()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var item = (await (await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Water", null, null, null, 15m, null, PricingType.Unit))).Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;

        var before = (await client.GetAsync("/items")).Headers.ETag!.ToString();

        var update = await client.PutAsJsonAsync(
            $"/items/{item.Id}",
            new UpdateItemRequest("Water", null, null, null, 18m, null, true));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var stale = await SendWithIfNoneMatchAsync(client, "/items", before);

        Assert.Equal(HttpStatusCode.OK, stale.StatusCode);
        Assert.NotEqual(before, stale.Headers.ETag!.ToString());
        var items = await stale.Content.ReadFromJsonAsync<List<ItemDto>>(JsonOptions);
        Assert.Equal(18m, items!.Single().BasePrice);
    }

    [Fact]
    public async Task Categories_list_uses_its_own_etag_and_ignores_item_changes()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        _ = await client.PostAsJsonAsync("/categories", new CreateCategoryRequest("Drinks", 0));

        var categoriesEtag = (await client.GetAsync("/categories")).Headers.ETag!.ToString();
        _ = await client.PostAsJsonAsync("/items", new CreateItemRequest("Water", null, null, null, 15m, null, PricingType.Unit));

        var afterItemChange = await SendWithIfNoneMatchAsync(client, "/categories", categoriesEtag);
        Assert.Equal(HttpStatusCode.NotModified, afterItemChange.StatusCode);

        _ = await client.PostAsJsonAsync("/categories", new CreateCategoryRequest("Snacks", 1));
        var afterCategoryChange = await SendWithIfNoneMatchAsync(client, "/categories", categoriesEtag);
        Assert.Equal(HttpStatusCode.OK, afterCategoryChange.StatusCode);
    }

    [Fact]
    public async Task Two_tenants_never_share_an_etag_for_different_catalogs()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var tenantA = await AuthenticatedAdminClientAsync(factory);
        using var tenantB = await AuthenticatedAdminClientAsync(factory);
        _ = await tenantA.PostAsJsonAsync("/items", new CreateItemRequest("Only A", null, null, null, 1m, null, PricingType.Unit));

        var etagA = (await tenantA.GetAsync("/items")).Headers.ETag!.ToString();

        var bWithAsTag = await SendWithIfNoneMatchAsync(tenantB, "/items", etagA);

        Assert.Equal(HttpStatusCode.OK, bWithAsTag.StatusCode);
        Assert.Empty((await bWithAsTag.Content.ReadFromJsonAsync<List<ItemDto>>(JsonOptions))!);
    }

    [Fact]
    public async Task Modifier_groups_list_returns_an_etag_and_answers_304_when_nothing_changed()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        _ = await client.PostAsJsonAsync("/modifier-groups", new CreateModifierGroupRequest("Sweetness", false, true));

        var first = await client.GetAsync("/modifier-groups");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var etag = first.Headers.ETag;
        Assert.NotNull(etag);
        Assert.Contains("no-cache", first.Headers.CacheControl!.ToString());

        var revalidate = await SendWithIfNoneMatchAsync(client, "/modifier-groups", etag!.ToString());

        Assert.Equal(HttpStatusCode.NotModified, revalidate.StatusCode);
        Assert.Equal(etag.ToString(), revalidate.Headers.ETag!.ToString());
        Assert.Empty(await revalidate.Content.ReadAsByteArrayAsync());
    }

    private static Task<HttpResponseMessage> SendWithIfNoneMatchAsync(HttpClient client, string path, string etag)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.TryAddWithoutValidation("If-None-Match", etag);
        return client.SendAsync(request);
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
