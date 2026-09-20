using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Purch.Application.Auth;
using Purch.Common.TestUtilities;
using Purch.Domain.Entities;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;
using Purch.Infrastructure.Auth;
using Purch.Infrastructure.Persistence;

namespace Purch.IntegrationTests;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class AuthEndpointsTests(PostgresContainerFixture postgres)
{
    private static readonly BCryptPinHasher PinHasher = new();
    private static readonly BCryptPasswordHasher PasswordHasher = new();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Login_with_correct_pairing_code_and_pin_issues_a_token_with_the_right_tenant_claim()
    {
        var tenantId = Guid.NewGuid();
        const string pairingCode = "DEVICE-CORRECT-PIN";
        const string pin = "1234";
        await SeedTenantDeviceAndUserAsync(tenantId, pairingCode, pin, Role.Admin);

        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/login", new LoginRequest(pairingCode, pin));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponseBody>(JsonOptions);
        Assert.NotNull(body);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(body!.AccessToken);
        var tenantClaim = jwt.Claims.Single(c => c.Type == JwtClaimTypes.TenantId).Value;
        var roleClaim = jwt.Claims.Single(c => c.Type == JwtClaimTypes.Role).Value;

        Assert.Equal(tenantId.ToString(), tenantClaim);
        Assert.Equal(nameof(Role.Admin), roleClaim);
        Assert.False(string.IsNullOrWhiteSpace(body!.RefreshToken));
    }

    [Fact]
    public async Task Refresh_with_a_valid_token_issues_a_new_access_token_and_rotates_the_refresh_token()
    {
        var tenantId = Guid.NewGuid();
        const string pairingCode = "DEVICE-REFRESH";
        const string pin = "1234";
        await SeedTenantDeviceAndUserAsync(tenantId, pairingCode, pin, Role.Admin);

        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/auth/login", new LoginRequest(pairingCode, pin));
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<LoginResponseBody>(JsonOptions);
        Assert.NotNull(loginBody);

        var refreshResponse = await client.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest(loginBody!.RefreshToken));

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var refreshBody = await refreshResponse.Content.ReadFromJsonAsync<LoginResponseBody>(JsonOptions);
        Assert.NotNull(refreshBody);
        Assert.False(string.IsNullOrWhiteSpace(refreshBody!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(refreshBody.RefreshToken));
        Assert.NotEqual(loginBody.RefreshToken, refreshBody.RefreshToken);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(refreshBody.AccessToken);
        Assert.Equal(tenantId.ToString(), jwt.Claims.Single(c => c.Type == JwtClaimTypes.TenantId).Value);

        // A just-rotated token is honoured for a short grace window, so a client that never received
        // (or never saved) the response to its first refresh can retry instead of being logged out.
        var retryResponse = await client.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest(loginBody.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, retryResponse.StatusCode);
        var retryBody = await retryResponse.Content.ReadFromJsonAsync<LoginResponseBody>(JsonOptions);
        Assert.False(string.IsNullOrWhiteSpace(retryBody!.RefreshToken));
    }

    [Fact]
    public async Task A_logged_out_refresh_token_can_never_be_redeemed_not_even_within_the_rotation_grace_window()
    {
        var tenantId = Guid.NewGuid();
        const string pairingCode = "DEVICE-LOGOUT";
        const string pin = "1234";
        await SeedTenantDeviceAndUserAsync(tenantId, pairingCode, pin, Role.Admin);

        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/auth/login", new LoginRequest(pairingCode, pin));
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<LoginResponseBody>(JsonOptions);

        // Rotate once, then log out with the original (now rotated) token as well as the new one.
        var rotated = await (await client.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest(loginBody!.RefreshToken)))
            .Content.ReadFromJsonAsync<LoginResponseBody>(JsonOptions);
        _ = await client.PostAsJsonAsync("/auth/logout", new RefreshTokenRequest(loginBody.RefreshToken));
        _ = await client.PostAsJsonAsync("/auth/logout", new RefreshTokenRequest(rotated!.RefreshToken));

        var afterLogoutOriginal = await client.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest(loginBody.RefreshToken));
        var afterLogoutRotated = await client.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest(rotated.RefreshToken));

        Assert.Equal(HttpStatusCode.Unauthorized, afterLogoutOriginal.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogoutRotated.StatusCode);
    }

    [Fact]
    public async Task Refresh_with_an_unrecognized_token_is_rejected()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest("not-a-real-refresh-token"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.False(string.IsNullOrWhiteSpace(problem?.Detail));
    }

    [Fact]
    public async Task Login_with_wrong_pin_is_rejected()
    {
        var tenantId = Guid.NewGuid();
        const string pairingCode = "DEVICE-WRONG-PIN";
        await SeedTenantDeviceAndUserAsync(tenantId, pairingCode, "1234", Role.Cashier);

        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/login", new LoginRequest(pairingCode, "9999"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.False(string.IsNullOrWhiteSpace(problem?.Detail));
    }

    [Fact]
    public async Task Login_with_unknown_device_pairing_code_is_rejected()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/login", new LoginRequest("NO-SUCH-DEVICE", "1234"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.False(string.IsNullOrWhiteSpace(problem?.Detail));
    }

    [Fact]
    public async Task Login_with_empty_pin_returns_400_with_a_field_level_error()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/login", new LoginRequest("SOME-DEVICE", string.Empty));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Contains("Pin", problem!.Errors.Keys);
    }

    [Fact]
    public async Task An_admin_login_token_has_no_device_so_a_device_endpoint_answers_403_not_500()
    {
        var tenantId = Guid.NewGuid();
        const string email = "web-admin@example.com";
        const string password = "web-admin-password-123";
        _ = await SeedAdminUserAsync(tenantId, email, password);

        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();
        var loginResponse = await client.PostAsJsonAsync("/auth/admin-login", new AdminLoginRequest(email, password));
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<LoginResponseBody>(JsonOptions);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginBody!.AccessToken);

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
                new Purch.Application.Onboarding.BootstrapTenantRequest($"Tenant-{Guid.NewGuid():N}", BusinessType.ConvenienceStore, "Main", "Admin", "1234"));
            statuses.Add(response.StatusCode);
        }

        Assert.All(statuses.Take(10), status => Assert.Equal(HttpStatusCode.OK, status));
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[10]);
    }

    [Fact]
    public async Task Requesting_and_confirming_a_password_reset_lets_the_admin_log_in_with_the_new_password_and_revokes_old_sessions()
    {
        var tenantId = Guid.NewGuid();
        const string email = "owner@example.com";
        const string oldPassword = "old-password-123";
        const string newPassword = "brand-new-password-456";
        var userId = await SeedAdminUserAsync(tenantId, email, oldPassword);

        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        var oldLoginResponse = await client.PostAsJsonAsync("/auth/admin-login", new AdminLoginRequest(email, oldPassword));
        var oldLoginBody = await oldLoginResponse.Content.ReadFromJsonAsync<LoginResponseBody>(JsonOptions);
        Assert.NotNull(oldLoginBody);

        var requestResponse = await client.PostAsJsonAsync("/auth/password-reset/request", new PasswordResetRequest(email));
        Assert.Equal(HttpStatusCode.NoContent, requestResponse.StatusCode);

        var rawToken = factory.PasswordResetTokenNotifier.LastTokenFor(userId);

        var confirmResponse = await client.PostAsJsonAsync(
            "/auth/password-reset/confirm",
            new PasswordResetConfirmRequest(rawToken, newPassword));
        Assert.Equal(HttpStatusCode.NoContent, confirmResponse.StatusCode);

        // The refresh token from before the reset no longer works...
        var refreshWithOldTokenResponse = await client.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest(oldLoginBody!.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refreshWithOldTokenResponse.StatusCode);

        // ...the old password no longer works...
        var oldPasswordLoginResponse = await client.PostAsJsonAsync("/auth/admin-login", new AdminLoginRequest(email, oldPassword));
        Assert.Equal(HttpStatusCode.Unauthorized, oldPasswordLoginResponse.StatusCode);

        // ...but the new one does.
        var newPasswordLoginResponse = await client.PostAsJsonAsync("/auth/admin-login", new AdminLoginRequest(email, newPassword));
        Assert.Equal(HttpStatusCode.OK, newPasswordLoginResponse.StatusCode);
    }

    [Fact]
    public async Task Confirming_a_password_reset_with_an_unrecognized_token_is_rejected()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/auth/password-reset/confirm",
            new PasswordResetConfirmRequest("not-a-real-token", "whatever-password"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Requesting_a_password_reset_for_an_unknown_email_still_returns_204()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        // Never reveals whether the email has an account — same shape either way.
        var response = await client.PostAsJsonAsync("/auth/password-reset/request", new PasswordResetRequest("nobody@example.com"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private async Task<Guid> SeedAdminUserAsync(Guid tenantId, string email, string password)
    {
        var options = new DbContextOptionsBuilder<PurchDbContext>()
            .UseNpgsql(postgres.ConnectionString)
            .Options;

        await using var dbContext = new PurchDbContext(options, new TestCurrentTenantProvider { TenantId = tenantId });

        var user = new User
        {
            TenantId = tenantId,
            Name = "Test Owner",
            Email = email,
            Role = Role.Admin,
            ScopeType = ScopeType.Tenant,
            PinHash = PinHasher.Hash("0000"),
            PasswordHash = PasswordHasher.Hash(password),
            IsActive = true,
        };
        _ = dbContext.Users.Add(user);

        _ = await dbContext.SaveChangesAsync();

        return user.Id;
    }

    private async Task SeedTenantDeviceAndUserAsync(Guid tenantId, string pairingCode, string pin, Role role)
    {
        var options = new DbContextOptionsBuilder<PurchDbContext>()
            .UseNpgsql(postgres.ConnectionString)
            .Options;

        await using var dbContext = new PurchDbContext(options, new TestCurrentTenantProvider { TenantId = tenantId });

        var branch = new Branch { TenantId = tenantId, Name = "Main Branch" };
        _ = dbContext.Branches.Add(branch);

        _ = dbContext.Devices.Add(new Device
        {
            TenantId = tenantId,
            BranchId = branch.Id,
            PairingCode = pairingCode,
        });

        _ = dbContext.Users.Add(new User
        {
            TenantId = tenantId,
            Name = "Test Staff",
            Role = role,
            ScopeType = ScopeType.Tenant,
            PinHash = PinHasher.Hash(pin),
            IsActive = true,
        });

        _ = await dbContext.SaveChangesAsync();
    }

    private sealed record LoginResponseBody(string AccessToken, string RefreshToken);
}
