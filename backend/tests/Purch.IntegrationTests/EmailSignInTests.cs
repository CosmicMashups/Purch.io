using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Purch.Application.Auth;
using Purch.Application.Onboarding;
using Purch.Common.TestUtilities;
using Purch.Domain.Entities;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;
using Purch.Infrastructure.Auth;
using Purch.Infrastructure.Persistence;

namespace Purch.IntegrationTests;

/// <summary>Sign-in with email and password: the identity provider checks the password, then the API issues its own tokens
/// from the person's membership. These tests run against the local identity provider (see PurchApiFactory).</summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class EmailSignInTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string Password = "correct horse battery";

    private sealed record Tokens(string AccessToken, string RefreshToken);

    private sealed record Business(Guid TenantId, string Name);

    private sealed record Choice(bool ChooseBusiness, List<Business> Businesses);

    private static string NewEmail() => $"{Guid.NewGuid():N}@example.com";

    private PurchDbContext NewContext(Guid? tenantId = null)
    {
        var options = new DbContextOptionsBuilder<PurchDbContext>().UseNpgsql(postgres.ConnectionString).Options;
        return new PurchDbContext(options, new TestCurrentTenantProvider { TenantId = tenantId });
    }

    private static async Task<BootstrapTenantResult> BootstrapAsync(HttpClient client, string email, string password = Password, string name = "Ana's Store")
    {
        var response = await client.PostAsJsonAsync(
            "/onboarding/bootstrap",
            new BootstrapTenantRequest($"{name} {Guid.NewGuid():N}", BusinessType.ConvenienceStore, "Main Branch", "Ana Reyes", "1234", email, password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<BootstrapTenantResult>(JsonOptions))!;
    }

    private static async Task<string> WithoutTraceIdAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return string.Join('|', body.RootElement.EnumerateObject().Where(p => p.Name != "traceId").Select(p => $"{p.Name}={p.Value}"));
    }

    private static JwtSecurityToken Read(string accessToken) => new JwtSecurityTokenHandler().ReadJwtToken(accessToken);

    [Fact]
    public async Task The_owner_registered_with_an_email_signs_in_as_admin_on_a_personal_device()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();
        var email = NewEmail();
        var business = await BootstrapAsync(client, email);

        var response = await client.PostAsJsonAsync("/auth/sign-in", new SignInRequest(email.ToUpperInvariant(), Password));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tokens = (await response.Content.ReadFromJsonAsync<Tokens>(JsonOptions))!;
        var jwt = Read(tokens.AccessToken);
        Assert.Equal(business.TenantId.ToString(), jwt.Claims.Single(c => c.Type == JwtClaimTypes.TenantId).Value);
        Assert.Equal(nameof(Role.Admin), jwt.Claims.Single(c => c.Type == JwtClaimTypes.Role).Value);
        Assert.DoesNotContain(jwt.Claims, c => c.Type == JwtClaimTypes.DeviceId);
        Assert.False(string.IsNullOrWhiteSpace(tokens.RefreshToken));
    }

    [Fact]
    public async Task A_wrong_password_and_an_unknown_email_are_refused_the_same_way()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();
        var email = NewEmail();
        _ = await BootstrapAsync(client, email);

        var wrongPassword = await client.PostAsJsonAsync("/auth/sign-in", new SignInRequest(email, "not the password"));
        var unknown = await client.PostAsJsonAsync("/auth/sign-in", new SignInRequest(NewEmail(), Password));

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(await WithoutTraceIdAsync(wrongPassword), await WithoutTraceIdAsync(unknown));
    }

    [Fact]
    public async Task The_refresh_token_from_signing_in_rotates_and_keeps_the_same_membership()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();
        var email = NewEmail();
        var business = await BootstrapAsync(client, email);
        var tokens = (await (await client.PostAsJsonAsync("/auth/sign-in", new SignInRequest(email, Password))).Content.ReadFromJsonAsync<Tokens>(JsonOptions))!;

        var refreshed = await client.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest(tokens.RefreshToken));

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var next = (await refreshed.Content.ReadFromJsonAsync<Tokens>(JsonOptions))!;
        Assert.NotEqual(tokens.RefreshToken, next.RefreshToken);
        Assert.Equal(business.TenantId.ToString(), Read(next.AccessToken).Claims.Single(c => c.Type == JwtClaimTypes.TenantId).Value);
    }

    [Fact]
    public async Task One_email_in_two_businesses_is_asked_which_one_and_then_signs_in_to_it()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();
        var email = NewEmail();
        var first = await BootstrapAsync(client, email, name: "First");
        var second = await BootstrapAsync(client, email, name: "Second");

        var asked = await client.PostAsJsonAsync("/auth/sign-in", new SignInRequest(email, Password));
        var choice = (await asked.Content.ReadFromJsonAsync<Choice>(JsonOptions))!;
        Assert.True(choice.ChooseBusiness);
        Assert.Equal(new[] { first.TenantId, second.TenantId }.Order(), choice.Businesses.Select(b => b.TenantId).Order());

        var chosen = await client.PostAsJsonAsync("/auth/sign-in", new SignInRequest(email, Password, second.TenantId));
        var tokens = (await chosen.Content.ReadFromJsonAsync<Tokens>(JsonOptions))!;
        Assert.Equal(second.TenantId.ToString(), Read(tokens.AccessToken).Claims.Single(c => c.Type == JwtClaimTypes.TenantId).Value);

        var notMine = await client.PostAsJsonAsync("/auth/sign-in", new SignInRequest(email, Password, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Unauthorized, notMine.StatusCode);
    }

    [Fact]
    public async Task Registering_another_business_with_an_existing_email_needs_that_accounts_password()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();
        var email = NewEmail();
        _ = await BootstrapAsync(client, email);

        var response = await client.PostAsJsonAsync(
            "/onboarding/bootstrap",
            new BootstrapTenantRequest($"Other {Guid.NewGuid():N}", BusinessType.ConvenienceStore, "Main", "Someone Else", "1234", email, "a different password"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_deactivated_membership_cannot_sign_in_or_keep_refreshing()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();
        var email = NewEmail();
        var business = await BootstrapAsync(client, email);
        var tokens = (await (await client.PostAsJsonAsync("/auth/sign-in", new SignInRequest(email, Password))).Content.ReadFromJsonAsync<Tokens>(JsonOptions))!;

        await using (var db = NewContext(business.TenantId))
        {
            var membership = await db.Memberships.SingleAsync();
            membership.IsActive = false;
            _ = await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/auth/sign-in", new SignInRequest(email, Password))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest(tokens.RefreshToken))).StatusCode);
    }

    [Theory]
    [InlineData(StaffDuty.Warehouse, nameof(Role.Warehouse))]
    [InlineData(StaffDuty.Cashier, nameof(Role.Cashier))]
    [InlineData(StaffDuty.Cashier | StaffDuty.Warehouse, nameof(Role.Warehouse))]
    [InlineData(StaffDuty.None, null)]
    [InlineData(StaffDuty.Kitchen, null)]
    public async Task A_staff_member_gets_the_api_role_their_duties_allow_and_none_if_they_have_no_duty(StaffDuty duties, string? expectedRole)
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();
        var owner = await BootstrapAsync(client, NewEmail());
        var staffEmail = NewEmail();
        var branchId = owner.BranchId;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var accounts = scope.ServiceProvider.GetRequiredService<IAccountService>();
            var db = scope.ServiceProvider.GetRequiredService<PurchDbContext>();
            var account = await accounts.CreateAccountAsync(staffEmail, "Staff", Password);
            _ = db.Memberships.Add(new Membership
            {
                TenantId = owner.TenantId,
                AccountId = account.Id,
                Role = MembershipRole.Staff,
                Duties = duties,
                Branches = [new MembershipBranch { TenantId = owner.TenantId, BranchId = branchId }],
            });
            _ = await db.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync("/auth/sign-in", new SignInRequest(staffEmail, Password));

        if (expectedRole is null)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            return;
        }

        var jwt = Read((await response.Content.ReadFromJsonAsync<Tokens>(JsonOptions))!.AccessToken);
        Assert.Equal(expectedRole, jwt.Claims.Single(c => c.Type == JwtClaimTypes.Role).Value);
        Assert.Equal(nameof(ScopeType.Branch), jwt.Claims.Single(c => c.Type == JwtClaimTypes.ScopeType).Value);
        Assert.Equal(branchId.ToString(), jwt.Claims.Single(c => c.Type == JwtClaimTypes.ScopeId).Value);
    }
}
