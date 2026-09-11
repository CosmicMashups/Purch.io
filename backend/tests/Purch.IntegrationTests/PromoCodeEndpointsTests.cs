using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Purch.Application.Auth;
using Purch.Application.Onboarding;
using Purch.Application.Promotions;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class PromoCodeEndpointsTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Creating_a_promo_code_makes_it_listable()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var createResponse = await client.PostAsJsonAsync(
            "/promo-codes",
            new CreatePromoCodeRequest("WELCOME20", PromoDiscountType.Percentage, 20m, null));

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<PromoCodeDto>(JsonOptions);
        Assert.Equal("WELCOME20", created!.Code);
        Assert.True(created.IsActive);

        var list = await client.GetFromJsonAsync<List<PromoCodeDto>>("/promo-codes", JsonOptions);
        Assert.Contains(list!, promoCode => promoCode.Code == "WELCOME20");
    }

    [Fact]
    public async Task Creating_a_duplicate_code_is_rejected()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        _ = await client.PostAsJsonAsync(
            "/promo-codes",
            new CreatePromoCodeRequest("WELCOME20", PromoDiscountType.Percentage, 20m, null));

        var response = await client.PostAsJsonAsync(
            "/promo-codes",
            new CreatePromoCodeRequest("welcome20", PromoDiscountType.FixedAmount, 5m, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_percentage_discount_over_100_is_rejected()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var response = await client.PostAsJsonAsync(
            "/promo-codes",
            new CreatePromoCodeRequest("TOOMUCH", PromoDiscountType.Percentage, 150m, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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
