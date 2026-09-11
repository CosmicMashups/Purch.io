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
public sealed class CatalogEndpointsTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Admin_can_create_a_category_and_an_item_in_it()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var categoryResponse = await client.PostAsJsonAsync("/categories", new CreateCategoryRequest("Beverages", 0));
        Assert.Equal(HttpStatusCode.OK, categoryResponse.StatusCode);
        var category = await categoryResponse.Content.ReadFromJsonAsync<CategoryDto>(JsonOptions);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Bottled Water", "SKU-1", "4800000000012", category!.Id, 15.00m, null, PricingType.Unit));

        Assert.Equal(HttpStatusCode.OK, itemResponse.StatusCode);
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);
        Assert.Equal(category.Id, item!.CategoryId);
        Assert.Equal(PricingType.Unit, item.PricingType);
    }

    [Fact]
    public async Task Creating_an_item_with_a_category_from_another_tenant_returns_404()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var response = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Ghost Item", null, null, Guid.NewGuid(), 10m, null, PricingType.Unit));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_negative_price_is_rejected_with_400()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var response = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Broken Item", null, null, null, -1m, null, PricingType.Unit));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_barcodes_within_the_same_tenant_are_rejected_with_409()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var first = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Item A", null, "SAME-BARCODE", null, 10m, null, PricingType.Unit));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Item B", null, "SAME-BARCODE", null, 20m, null, PricingType.Unit));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task When_the_tenant_requires_a_barcode_creating_an_item_without_one_is_rejected()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var barcodeSettingResponse = await client.PutAsJsonAsync(
            "/tenant/settings/barcode",
            new UpdateBarcodeSettingRequest(true));
        Assert.Equal(HttpStatusCode.OK, barcodeSettingResponse.StatusCode);

        var response = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("No Barcode Item", null, null, null, 10m, null, PricingType.Unit));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_cashier_can_list_items_but_cannot_create_one()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var adminClient = await AuthenticatedAdminClientAsync(factory);

        var createStaffResponse = await adminClient.PostAsJsonAsync(
            "/staff",
            new CreateStaffRequest("Cashier One", Role.Cashier, ScopeType.Tenant, null, null, "5678"));
        Assert.Equal(HttpStatusCode.OK, createStaffResponse.StatusCode);

        var devices = await adminClient.GetFromJsonAsync<List<DeviceDto>>("/devices", JsonOptions);
        using var cashierClient = factory.CreateClient();
        var loginResponse = await cashierClient.PostAsJsonAsync(
            "/auth/login",
            new LoginRequest(devices!.Single().PairingCode, "5678"));
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<LoginResponseBody>(JsonOptions);
        cashierClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginBody!.AccessToken);

        var listResponse = await cashierClient.GetAsync("/items");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var createResponse = await cashierClient.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Unauthorized Item", null, null, null, 10m, null, PricingType.Unit));
        Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);
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
