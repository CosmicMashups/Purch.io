using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Purch.Application.Onboarding;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class ParkedCartTtlSettingTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static async Task<TenantSettingsDto> ReadAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TenantSettingsDto>(JsonOptions))!;
    }

    [Fact]
    public async Task Ttl_defaults_to_24_can_be_set_and_an_omitted_value_keeps_it()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);

        Assert.Equal(24, (await ReadAsync(await admin.GetAsync("/tenant/settings"))).ParkedCartTtlHours);

        var set = await ReadAsync(await admin.PutAsJsonAsync("/tenant/settings/barcode", new { requiresBarcodePerItem = false, parkedCartTtlHours = 72 }));
        Assert.Equal(72, set.ParkedCartTtlHours);

        var kept = await ReadAsync(await admin.PutAsJsonAsync("/tenant/settings/barcode", new { requiresBarcodePerItem = true }));
        Assert.Equal(72, kept.ParkedCartTtlHours);
        Assert.Equal(72, (await ReadAsync(await admin.GetAsync("/tenant/settings"))).ParkedCartTtlHours);

        Assert.Equal(1, (await ReadAsync(await admin.PutAsJsonAsync("/tenant/settings/barcode", new { requiresBarcodePerItem = true, parkedCartTtlHours = 1 }))).ParkedCartTtlHours);
        Assert.Equal(168, (await ReadAsync(await admin.PutAsJsonAsync("/tenant/settings/barcode", new { requiresBarcodePerItem = true, parkedCartTtlHours = 168 }))).ParkedCartTtlHours);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(169)]
    [InlineData(-1)]
    public async Task Out_of_range_ttl_is_400_and_changes_nothing(int hours)
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);

        var response = await admin.PutAsJsonAsync("/tenant/settings/barcode", new { requiresBarcodePerItem = true, parkedCartTtlHours = hours });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(24, (await ReadAsync(await admin.GetAsync("/tenant/settings"))).ParkedCartTtlHours);
    }

    [Fact]
    public async Task A_cashier_cannot_change_the_ttl()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        using var cashier = await TestSessions.CashierClientAsync(admin);

        var response = await cashier.PutAsJsonAsync("/tenant/settings/barcode", new { requiresBarcodePerItem = true, parkedCartTtlHours = 48 });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
