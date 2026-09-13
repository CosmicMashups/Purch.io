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
public sealed class DepartmentEndpointsTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Admin_can_create_a_department_for_a_branch_and_list_it()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var branchId = await FirstBranchIdAsync(client);

        var departmentResponse = await client.PostAsJsonAsync(
            $"/branches/{branchId}/departments",
            new CreateDepartmentRequest("Bakery Stall", "Juan Dela Cruz, 0917-000-0000"));

        Assert.Equal(HttpStatusCode.OK, departmentResponse.StatusCode);
        var department = await departmentResponse.Content.ReadFromJsonAsync<DepartmentDto>(JsonOptions);
        Assert.Equal("Bakery Stall", department!.Name);
        Assert.Equal(branchId, department.BranchId);

        var listResponse = await client.GetAsync($"/branches/{branchId}/departments");
        var departments = await listResponse.Content.ReadFromJsonAsync<List<DepartmentDto>>(JsonOptions);
        _ = Assert.Single(departments!);
    }

    [Fact]
    public async Task Creating_a_department_for_a_nonexistent_branch_is_rejected()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var departmentResponse = await client.PostAsJsonAsync(
            $"/branches/{Guid.NewGuid()}/departments",
            new CreateDepartmentRequest("Ghost Stall", null));

        Assert.Equal(HttpStatusCode.NotFound, departmentResponse.StatusCode);
    }

    [Fact]
    public async Task Admin_can_assign_an_item_to_a_department_on_creation()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var branchId = await FirstBranchIdAsync(client);

        var departmentResponse = await client.PostAsJsonAsync(
            $"/branches/{branchId}/departments",
            new CreateDepartmentRequest("Fruit Stand", null));
        var department = await departmentResponse.Content.ReadFromJsonAsync<DepartmentDto>(JsonOptions);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Mangoes", null, null, null, 120m, null, PricingType.WeightVolume, department!.Id));

        Assert.Equal(HttpStatusCode.OK, itemResponse.StatusCode);
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);
        Assert.Equal(department.Id, item!.DepartmentId);
    }

    [Fact]
    public async Task Assigning_an_item_to_a_nonexistent_department_is_rejected()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Ghost Item", null, null, null, 10m, null, PricingType.Unit, Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, itemResponse.StatusCode);
    }

    [Fact]
    public async Task Admin_can_assign_and_later_clear_an_items_department()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);
        var branchId = await FirstBranchIdAsync(client);

        var departmentResponse = await client.PostAsJsonAsync(
            $"/branches/{branchId}/departments",
            new CreateDepartmentRequest("Electronics Stall", null));
        var department = await departmentResponse.Content.ReadFromJsonAsync<DepartmentDto>(JsonOptions);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Headphones", null, null, null, 499m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        var assignResponse = await client.PutAsJsonAsync(
            $"/items/{item!.Id}/department",
            new UpdateItemDepartmentRequest(department!.Id));
        var assigned = await assignResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);
        Assert.Equal(department.Id, assigned!.DepartmentId);

        var clearResponse = await client.PutAsJsonAsync(
            $"/items/{item.Id}/department",
            new UpdateItemDepartmentRequest(null));
        var cleared = await clearResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);
        Assert.Null(cleared!.DepartmentId);
    }

    private static async Task<Guid> FirstBranchIdAsync(HttpClient client)
    {
        var branchesResponse = await client.GetAsync("/branches");
        var branches = await branchesResponse.Content.ReadFromJsonAsync<List<BranchDto>>(JsonOptions);
        return branches!.Single().Id;
    }

    private static async Task<HttpClient> AuthenticatedAdminClientAsync(PurchApiFactory factory)
    {
        var client = factory.CreateClient();

        var bootstrapResponse = await client.PostAsJsonAsync(
            "/onboarding/bootstrap",
            new BootstrapTenantRequest(
                $"Tenant-{Guid.NewGuid():N}",
                BusinessType.SariSariStore,
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
