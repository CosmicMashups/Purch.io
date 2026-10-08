using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Purch.Application.Auth;
using Purch.Application.Onboarding;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class RegisterSessionTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record Tokens(string AccessToken, string RefreshToken);

    [Fact]
    public async Task Admin_signed_in_by_email_gets_a_register_session_without_pairing_a_device()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var anonymous = factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@example.com";
        var bootstrap = await anonymous.PostAsJsonAsync(
            "/onboarding/bootstrap",
            new BootstrapTenantRequest($"Tenant-{Guid.NewGuid():N}", BusinessType.ConvenienceStore, "Main Branch", "Admin User", TestSessions.AdminPin, email, TestSessions.Password));
        bootstrap.EnsureSuccessStatusCode();
        var tenant = (await bootstrap.Content.ReadFromJsonAsync<BootstrapTenantResult>(JsonOptions))!;

        var signIn = await anonymous.PostAsJsonAsync("/auth/sign-in", new SignInRequest(email, TestSessions.Password, tenant.TenantId));
        var personal = (await signIn.Content.ReadFromJsonAsync<Tokens>(JsonOptions))!;
        using var personalClient = TestSessions.Bearer(factory, personal.AccessToken);

        var started = await personalClient.PostAsJsonAsync("/auth/register-session", new RegisterSessionRequest());

        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        var session = (await started.Content.ReadFromJsonAsync<Tokens>(JsonOptions))!;
        Assert.False(string.IsNullOrEmpty(session.AccessToken));

        // The business had no Register; one was made and is already active.
        using var sessionClient = TestSessions.Bearer(factory, session.AccessToken);
        var devices = (await sessionClient.GetFromJsonAsync<List<DeviceDto>>("/devices", JsonOptions))!;
        var register = Assert.Single(devices);
        Assert.Equal(tenant.BranchId, register.BranchId);
        Assert.Equal(DeviceStatus.Active, register.Status);

        // A second request reuses it rather than making another.
        var again = await personalClient.PostAsJsonAsync("/auth/register-session", new RegisterSessionRequest());
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Single((await sessionClient.GetFromJsonAsync<List<DeviceDto>>("/devices", JsonOptions))!);
    }
}
