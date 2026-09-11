using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Purch.Application.Auth;
using Purch.Application.Catalog;
using Purch.Application.Onboarding;
using Purch.Application.Pos;
using Purch.Application.Shifts;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class ShiftEndpointsTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Getting_the_current_shift_with_none_open_returns_null()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var response = await client.GetAsync("/shifts/current");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal("null", body);
    }

    [Fact]
    public async Task Opening_a_shift_makes_it_the_current_shift()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var openResponse = await client.PostAsJsonAsync("/shifts/open", new OpenShiftRequest(1000m));
        Assert.Equal(HttpStatusCode.OK, openResponse.StatusCode);
        var opened = await openResponse.Content.ReadFromJsonAsync<ShiftDto>(JsonOptions);
        Assert.Equal(ShiftStatus.Open, opened!.Status);
        Assert.Equal(1000m, opened.OpeningCashAmount);

        var current = await client.GetFromJsonAsync<ShiftDto>("/shifts/current", JsonOptions);
        Assert.Equal(opened.Id, current!.Id);
    }

    [Fact]
    public async Task Opening_a_second_shift_on_the_same_device_is_rejected()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        _ = await client.PostAsJsonAsync("/shifts/open", new OpenShiftRequest(500m));

        var response = await client.PostAsJsonAsync("/shifts/open", new OpenShiftRequest(500m));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Closing_a_shift_with_a_matching_count_needs_no_approval()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        _ = await client.PostAsJsonAsync("/shifts/open", new OpenShiftRequest(1000m));

        var closeResponse = await client.PostAsJsonAsync(
            "/shifts/close",
            new CloseShiftRequest(1000m, "Nothing unusual.", null));

        Assert.Equal(HttpStatusCode.OK, closeResponse.StatusCode);
        var closed = await closeResponse.Content.ReadFromJsonAsync<ShiftDto>(JsonOptions);
        Assert.Equal(ShiftStatus.Closed, closed!.Status);
        Assert.Equal(1000m, closed.ExpectedCashAmount);
        Assert.Equal(0m, closed.VarianceAmount);
        Assert.Null(closed.ApprovedByUserId);
    }

    [Fact]
    public async Task Closing_a_shift_folds_in_cash_sales_recorded_during_it()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        _ = await client.PostAsJsonAsync("/shifts/open", new OpenShiftRequest(1000m));

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Bottled Water", null, null, null, 15m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);
        _ = await client.PostAsJsonAsync(
            "/transactions/cart/lines",
            new AddTransactionLineRequest(item!.Id, null, 1m));
        _ = await client.PostAsJsonAsync(
            "/transactions/cart/payments",
            new RecordPaymentRequest(PaymentMethod.Cash, 15m));

        var closeResponse = await client.PostAsJsonAsync(
            "/shifts/close",
            new CloseShiftRequest(1015m, null, null));
        var closed = await closeResponse.Content.ReadFromJsonAsync<ShiftDto>(JsonOptions);

        Assert.Equal(1015m, closed!.ExpectedCashAmount);
        Assert.Equal(0m, closed.VarianceAmount);
    }

    [Fact]
    public async Task Closing_a_shift_with_a_mismatched_count_and_no_approver_pin_is_rejected()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        _ = await client.PostAsJsonAsync("/shifts/open", new OpenShiftRequest(1000m));

        var response = await client.PostAsJsonAsync(
            "/shifts/close",
            new CloseShiftRequest(950m, null, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Closing_a_shift_with_a_mismatched_count_and_a_valid_manager_pin_records_the_approver()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var managerResponse = await client.PostAsJsonAsync(
            "/staff",
            new CreateStaffRequest("Manager Mae", Role.Manager, ScopeType.Tenant, null, null, "5678"));
        var manager = await managerResponse.Content.ReadFromJsonAsync<StaffDto>(JsonOptions);

        _ = await client.PostAsJsonAsync("/shifts/open", new OpenShiftRequest(1000m));

        var closeResponse = await client.PostAsJsonAsync(
            "/shifts/close",
            new CloseShiftRequest(950m, "Short by 50.", "5678"));

        Assert.Equal(HttpStatusCode.OK, closeResponse.StatusCode);
        var closed = await closeResponse.Content.ReadFromJsonAsync<ShiftDto>(JsonOptions);
        Assert.Equal(-50m, closed!.VarianceAmount);
        Assert.Equal(manager!.Id, closed.ApprovedByUserId);
    }

    [Fact]
    public async Task Closing_a_shift_with_a_mismatched_count_and_a_wrong_pin_is_rejected()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        _ = await client.PostAsJsonAsync("/shifts/open", new OpenShiftRequest(1000m));

        var response = await client.PostAsJsonAsync(
            "/shifts/close",
            new CloseShiftRequest(950m, null, "0000"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Closing_a_shift_leaves_no_open_shift_behind()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        _ = await client.PostAsJsonAsync("/shifts/open", new OpenShiftRequest(1000m));
        _ = await client.PostAsJsonAsync("/shifts/close", new CloseShiftRequest(1000m, null, null));

        var current = await client.GetAsync("/shifts/current");
        var body = await current.Content.ReadAsStringAsync();

        Assert.Equal("null", body);
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
