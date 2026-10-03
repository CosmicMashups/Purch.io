using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Purch.Application.Auth;
using Purch.Application.Onboarding;
using Purch.Common.TestUtilities;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;
using Purch.Infrastructure.Persistence;

namespace Purch.IntegrationTests;

/// <summary>Refresh tokens, logout and the limits on the anonymous sign-in endpoints. Signing in itself is covered by
/// EmailSignInTests, DevicePairingTests and DeviceUnlockTests.</summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class AuthEndpointsTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string Password = "correct horse battery";

    private sealed record Tokens(string AccessToken, string RefreshToken);

    /// <summary>A new business and its owner signed in with email and password. Returns the tokens and the tenant id.</summary>
    private static async Task<(Tokens Tokens, Guid TenantId, string Email)> OwnerAsync(HttpClient client)
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        var bootstrap = await client.PostAsJsonAsync(
            "/onboarding/bootstrap",
            new BootstrapTenantRequest($"Tenant-{Guid.NewGuid():N}", BusinessType.ConvenienceStore, "Main Branch", "Owner", "1234", email, Password));
        var tenant = (await bootstrap.Content.ReadFromJsonAsync<BootstrapTenantResult>(JsonOptions))!;
        var tokens = (await (await client.PostAsJsonAsync("/auth/sign-in", new SignInRequest(email, Password))).Content.ReadFromJsonAsync<Tokens>(JsonOptions))!;
        return (tokens, tenant.TenantId, email);
    }

    [Fact]
    public async Task Refresh_with_a_valid_token_issues_a_new_access_token_and_rotates_the_refresh_token()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();
        var (tokens, _, _) = await OwnerAsync(client);

        var refreshResponse = await client.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest(tokens.RefreshToken));

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var refreshed = (await refreshResponse.Content.ReadFromJsonAsync<Tokens>(JsonOptions))!;
        Assert.False(string.IsNullOrWhiteSpace(refreshed.AccessToken));
        Assert.NotEqual(tokens.RefreshToken, refreshed.RefreshToken);

        // The rotated-out token is single-use: only a short grace window, for a lost response, lets it through again.
        var replayInsideGrace = await client.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest(tokens.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, replayInsideGrace.StatusCode);
    }

    [Fact]
    public async Task Replaying_a_spent_refresh_token_after_the_grace_window_ends_every_session_of_that_person()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();
        var (stolen, tenantId, _) = await OwnerAsync(client);

        // The legitimate client rotates it and carries on with the newer token.
        var rotated = (await (await client.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest(stolen.RefreshToken))).Content.ReadFromJsonAsync<Tokens>(JsonOptions))!;

        // Time passes beyond the grace window.
        var options = new DbContextOptionsBuilder<PurchDbContext>().UseNpgsql(postgres.ConnectionString).Options;
        await using (var dbContext = new PurchDbContext(options, new TestCurrentTenantProvider { TenantId = tenantId }))
        {
            var longAgo = DateTimeOffset.UtcNow.AddMinutes(-5);
            _ = await dbContext.RefreshTokens
                .Where(token => token.RevokedAt != null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.RevokedAt, longAgo));
        }

        // Someone replays the old copy...
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest(stolen.RefreshToken))).StatusCode);

        // ...and the newer token, which the legitimate client holds, is no longer trusted either.
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest(rotated.RefreshToken))).StatusCode);
    }

    [Fact]
    public async Task A_logged_out_refresh_token_can_never_be_redeemed_not_even_within_the_rotation_grace_window()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();
        var (original, _, _) = await OwnerAsync(client);

        // Rotate once, then log out with the original (now rotated) token as well as the new one.
        var rotated = (await (await client.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest(original.RefreshToken))).Content.ReadFromJsonAsync<Tokens>(JsonOptions))!;
        _ = await client.PostAsJsonAsync("/auth/logout", new RefreshTokenRequest(original.RefreshToken));
        _ = await client.PostAsJsonAsync("/auth/logout", new RefreshTokenRequest(rotated.RefreshToken));

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest(original.RefreshToken))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest(rotated.RefreshToken))).StatusCode);
    }

    [Fact]
    public async Task Refresh_with_an_unrecognized_token_is_rejected()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest("not-a-real-token"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_personal_device_token_has_no_device_so_a_till_endpoint_answers_403_not_500()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();
        var (tokens, _, _) = await OwnerAsync(client);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var response = await client.GetAsync("/transactions/cart");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Tenant_bootstrap_is_rate_limited_per_client()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var attempt = 0; attempt < 11; attempt++)
        {
            var response = await client.PostAsJsonAsync(
                "/onboarding/bootstrap",
                new BootstrapTenantRequest($"Tenant-{Guid.NewGuid():N}", BusinessType.ConvenienceStore, "Main", "Admin", "1234", $"{Guid.NewGuid():N}@example.com", Password));
            statuses.Add(response.StatusCode);
        }

        Assert.All(statuses.Take(10), status => Assert.Equal(HttpStatusCode.OK, status));
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[10]);
    }

    [Fact]
    public async Task Email_sign_in_attempts_are_capped_per_client()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var attempt = 0; attempt < 11; attempt++)
        {
            statuses.Add((await client.PostAsJsonAsync("/auth/sign-in", new SignInRequest("nobody@example.com", $"guess-{attempt}-guess"))).StatusCode);
        }

        Assert.All(statuses.Take(10), status => Assert.Equal(HttpStatusCode.Unauthorized, status));
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[10]);
    }

    [Fact]
    public async Task Refresh_attempts_are_capped_per_client_but_far_above_normal_use()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var attempt = 0; attempt < 121; attempt++)
        {
            var response = await client.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest($"not-a-real-token-{attempt}"));
            statuses.Add(response.StatusCode);
        }

        // Every terminal and tab renews on a timer, so 120 per 15 minutes must not trip a busy shop...
        Assert.All(statuses.Take(120), status => Assert.Equal(HttpStatusCode.Unauthorized, status));
        // ...but an endless token-guessing loop is cut off.
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[120]);
    }

    [Fact]
    public async Task The_old_pin_and_password_endpoints_no_longer_exist()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        foreach (var path in new[] { "/auth/login", "/auth/admin-login", "/auth/password-reset/request", "/auth/password-reset/confirm", "/kiosk/session", "/order-board/session", "/kitchen-display/session", "/warehouse-officer/session" })
        {
            var response = await client.PostAsJsonAsync(path, new { });
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed, $"{path} answered {response.StatusCode}");
        }
    }
}
