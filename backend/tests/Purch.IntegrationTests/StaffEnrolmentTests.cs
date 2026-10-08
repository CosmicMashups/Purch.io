using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Purch.Application.Auth;
using Purch.Api.Endpoints;
using Purch.Application.Onboarding;
using Purch.Common.TestUtilities;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;
using Purch.Infrastructure.Persistence;

namespace Purch.IntegrationTests;

/// <summary>An Admin or Manager invites a person; the person opens a single-use link, sets a password and a PIN, and is in.
/// No email is sent and none is verified, so the link is the only proof of the invitation.</summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class StaffEnrolmentTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string Password = "correct horse battery";

    private sealed record Tokens(string AccessToken, string RefreshToken);

    private sealed record Business(HttpClient Admin, BootstrapTenantResult Result, string AdminEmail);

    private static string NewEmail() => $"{Guid.NewGuid():N}@example.com";

    private static HttpClient As(PurchApiFactory factory, string? accessToken = null)
    {
        var client = factory.CreateClient();
        if (accessToken is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return client;
    }

    private static async Task<Business> NewBusinessAsync(PurchApiFactory factory, string? email = null)
    {
        email ??= NewEmail();
        using var anonymous = factory.CreateClient();
        var response = await anonymous.PostAsJsonAsync(
            "/onboarding/bootstrap",
            new BootstrapTenantRequest($"Store {Guid.NewGuid():N}", BusinessType.ConvenienceStore, "Main", "Ana", "123412", email, Password));
        var result = (await response.Content.ReadFromJsonAsync<BootstrapTenantResult>(JsonOptions))!;
        var tokens = (await (await anonymous.PostAsJsonAsync("/auth/sign-in", new SignInRequest(email, Password, result.TenantId))).Content.ReadFromJsonAsync<Tokens>(JsonOptions))!;
        return new Business(As(factory, tokens.AccessToken), result, email);
    }

    private static CreateInviteRequest StaffInvite(Business business, string? email = null, StaffDuty duties = StaffDuty.Cashier)
        => new("Ben Santos", email ?? NewEmail(), MembershipRole.Staff, duties, [business.Result.BranchId]);

    private static async Task<InviteLinkDto> InviteAsync(HttpClient inviter, CreateInviteRequest request)
    {
        var response = await inviter.PostAsJsonAsync("/staff/invites", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<InviteLinkDto>(JsonOptions))!;
    }

    private static Task<HttpResponseMessage> RedeemAsync(HttpClient anonymous, string token, string password = Password, string? pin = "482112")
        => anonymous.PostAsJsonAsync("/enrol/redeem", new RedeemInviteRequest(token, password, pin));

    private static JwtSecurityToken Read(string accessToken) => new JwtSecurityTokenHandler().ReadJwtToken(accessToken);

    private PurchDbContext NewContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<PurchDbContext>().UseNpgsql(postgres.ConnectionString).Options;
        return new PurchDbContext(options, new TestCurrentTenantProvider { TenantId = tenantId });
    }

    [Fact]
    public async Task A_staff_member_opens_the_link_sets_a_password_and_pin_and_is_signed_in_once_only()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var business = await NewBusinessAsync(factory);
        using var anonymous = factory.CreateClient();
        var email = NewEmail();
        var link = await InviteAsync(business.Admin, StaffInvite(business, email));

        var preview = await anonymous.PostAsJsonAsync("/enrol/preview", new TokenBody(link.Token));
        var shown = (await preview.Content.ReadFromJsonAsync<InvitePreviewDto>(JsonOptions))!;
        Assert.Equal(email, shown.Email);
        Assert.Equal(StaffDuty.Cashier, shown.Duties);
        Assert.False(shown.HasAccount);

        var redeemed = await RedeemAsync(anonymous, link.Token);
        Assert.Equal(HttpStatusCode.OK, redeemed.StatusCode);
        var jwt = Read((await redeemed.Content.ReadFromJsonAsync<Tokens>(JsonOptions))!.AccessToken);
        Assert.Equal(nameof(Role.Cashier), jwt.Claims.Single(c => c.Type == JwtClaimTypes.Role).Value);
        Assert.Equal(business.Result.TenantId.ToString(), jwt.Claims.Single(c => c.Type == JwtClaimTypes.TenantId).Value);
        Assert.Equal(business.Result.BranchId.ToString(), jwt.Claims.Single(c => c.Type == JwtClaimTypes.ScopeId).Value);

        // The password works for a normal sign-in, and the link cannot be used a second time.
        Assert.Equal(HttpStatusCode.OK, (await anonymous.PostAsJsonAsync("/auth/sign-in", new SignInRequest(email, Password))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await RedeemAsync(anonymous, link.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.PostAsJsonAsync("/enrol/preview", new TokenBody(link.Token))).StatusCode);

        var members = (await business.Admin.GetFromJsonAsync<List<MemberDto>>("/staff/members", JsonOptions))!;
        var member = members.Single(m => m.Email == email);
        Assert.True(member.HasPin);
        Assert.Equal([business.Result.BranchId], member.BranchIds);
        Assert.Empty(await business.Admin.GetFromJsonAsync<List<InviteDto>>("/staff/invites", JsonOptions) ?? []);
    }

    [Fact]
    public async Task An_invitation_needs_a_duty_a_branch_and_a_new_email()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var business = await NewBusinessAsync(factory);
        var admin = business.Admin;

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/staff/invites", StaffInvite(business, duties: StaffDuty.None))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/staff/invites", StaffInvite(business) with { BranchIds = [] })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/staff/invites", StaffInvite(business, "not-an-email"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync("/staff/invites", StaffInvite(business) with { BranchIds = [Guid.NewGuid()] })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/staff/invites", StaffInvite(business, business.AdminEmail))).StatusCode);
    }

    [Fact]
    public async Task The_password_and_pin_chosen_must_meet_the_rules()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var business = await NewBusinessAsync(factory);
        using var anonymous = factory.CreateClient();
        var link = await InviteAsync(business.Admin, StaffInvite(business));

        Assert.Equal(HttpStatusCode.BadRequest, (await RedeemAsync(anonymous, link.Token, "short")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await RedeemAsync(anonymous, link.Token, pin: "12")).StatusCode);
        // Four and five digits were fine under the old rule; a PIN being set now needs six.
        Assert.Equal(HttpStatusCode.BadRequest, (await RedeemAsync(anonymous, link.Token, pin: "4821")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await RedeemAsync(anonymous, link.Token, pin: "48211")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await RedeemAsync(anonymous, link.Token, pin: null)).StatusCode);

        // Nothing was used up by the refusals.
        Assert.Equal(HttpStatusCode.OK, (await RedeemAsync(anonymous, link.Token)).StatusCode);
    }

    [Fact]
    public async Task An_expired_or_cancelled_link_is_refused()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var business = await NewBusinessAsync(factory);
        using var anonymous = factory.CreateClient();
        var expired = await InviteAsync(business.Admin, StaffInvite(business));
        var cancelled = await InviteAsync(business.Admin, StaffInvite(business));

        await using (var db = NewContext(business.Result.TenantId))
        {
            var row = await db.EnrolmentInvites.SingleAsync(i => i.Id == expired.Invite.Id);
            row.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            _ = await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.NoContent, (await business.Admin.DeleteAsync($"/staff/invites/{cancelled.Invite.Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await RedeemAsync(anonymous, expired.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await RedeemAsync(anonymous, cancelled.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await RedeemAsync(anonymous, "not-a-real-token")).StatusCode);
    }

    [Fact]
    public async Task Two_people_opening_the_same_link_at_once_cannot_both_get_in()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var business = await NewBusinessAsync(factory);
        var link = await InviteAsync(business.Admin, StaffInvite(business));

        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => RedeemAsync(factory.CreateClient(), link.Token)));

        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.OK));
    }

    [Fact]
    public async Task Someone_who_already_has_an_account_joins_another_business_with_their_existing_password()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var first = await NewBusinessAsync(factory);
        var second = await NewBusinessAsync(factory);
        using var anonymous = factory.CreateClient();
        var email = NewEmail();
        var inFirst = await InviteAsync(first.Admin, StaffInvite(first, email));
        Assert.Equal(HttpStatusCode.OK, (await RedeemAsync(anonymous, inFirst.Token)).StatusCode);

        var inSecond = await InviteAsync(second.Admin, StaffInvite(second, email, StaffDuty.Warehouse));
        var preview = (await (await anonymous.PostAsJsonAsync("/enrol/preview", new TokenBody(inSecond.Token))).Content.ReadFromJsonAsync<InvitePreviewDto>(JsonOptions))!;
        Assert.True(preview.HasAccount);

        Assert.Equal(HttpStatusCode.BadRequest, (await RedeemAsync(anonymous, inSecond.Token, "not the password")).StatusCode);
        var joined = await RedeemAsync(anonymous, inSecond.Token);
        Assert.Equal(HttpStatusCode.OK, joined.StatusCode);
        Assert.Equal(nameof(Role.Warehouse), Read((await joined.Content.ReadFromJsonAsync<Tokens>(JsonOptions))!.AccessToken).Claims.Single(c => c.Type == JwtClaimTypes.Role).Value);

        // Two businesses now: sign-in asks which.
        var asked = await anonymous.PostAsJsonAsync("/auth/sign-in", new SignInRequest(email, Password));
        Assert.Contains("chooseBusiness", await asked.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_manager_can_invite_staff_but_not_managers_or_admins_and_cannot_touch_them()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var business = await NewBusinessAsync(factory);
        using var anonymous = factory.CreateClient();
        var managerEmail = NewEmail();
        var managerLink = await InviteAsync(business.Admin, new CreateInviteRequest("Maria", managerEmail, MembershipRole.Manager, StaffDuty.None, null));
        var managerTokens = (await (await RedeemAsync(anonymous, managerLink.Token)).Content.ReadFromJsonAsync<Tokens>(JsonOptions))!;
        Assert.Equal(nameof(Role.Manager), Read(managerTokens.AccessToken).Claims.Single(c => c.Type == JwtClaimTypes.Role).Value);
        var manager = As(factory, managerTokens.AccessToken);

        Assert.Equal(HttpStatusCode.OK, (await manager.PostAsJsonAsync("/staff/invites", StaffInvite(business))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsJsonAsync("/staff/invites", new CreateInviteRequest("X", NewEmail(), MembershipRole.Manager, StaffDuty.None, null))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsJsonAsync("/staff/invites", new CreateInviteRequest("X", NewEmail(), MembershipRole.Admin, StaffDuty.None, null))).StatusCode);

        var members = (await manager.GetFromJsonAsync<List<MemberDto>>("/staff/members", JsonOptions))!;
        var admin = members.Single(m => m.Role == MembershipRole.Admin);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsync($"/staff/members/{admin.Id}/reset-link", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PutAsJsonAsync($"/staff/members/{admin.Id}", new UpdateMemberRequest(MembershipRole.Admin, StaffDuty.None, null, false))).StatusCode);
    }

    [Fact]
    public async Task Changing_a_persons_duties_or_deactivating_them_ends_their_sessions_and_the_last_admin_is_protected()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var business = await NewBusinessAsync(factory);
        using var anonymous = factory.CreateClient();
        var email = NewEmail();
        var link = await InviteAsync(business.Admin, StaffInvite(business, email));
        var tokens = (await (await RedeemAsync(anonymous, link.Token)).Content.ReadFromJsonAsync<Tokens>(JsonOptions))!;
        var members = (await business.Admin.GetFromJsonAsync<List<MemberDto>>("/staff/members", JsonOptions))!;
        var staff = members.Single(m => m.Email == email);

        var updated = await business.Admin.PutAsJsonAsync($"/staff/members/{staff.Id}", new UpdateMemberRequest(MembershipRole.Staff, StaffDuty.Warehouse, [business.Result.BranchId], true));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(StaffDuty.Warehouse, (await updated.Content.ReadFromJsonAsync<MemberDto>(JsonOptions))!.Duties);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest(tokens.RefreshToken))).StatusCode);

        _ = await business.Admin.PutAsJsonAsync($"/staff/members/{staff.Id}", new UpdateMemberRequest(MembershipRole.Staff, StaffDuty.Warehouse, [business.Result.BranchId], false));
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/auth/sign-in", new SignInRequest(email, Password))).StatusCode);

        var admin = members.Single(m => m.Role == MembershipRole.Admin);
        var last = await business.Admin.PutAsJsonAsync($"/staff/members/{admin.Id}", new UpdateMemberRequest(MembershipRole.Admin, StaffDuty.None, null, false));
        Assert.Equal(HttpStatusCode.BadRequest, last.StatusCode);
    }

    [Fact]
    public async Task A_reset_link_sets_a_new_password_and_ends_the_old_sessions()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var business = await NewBusinessAsync(factory);
        using var anonymous = factory.CreateClient();
        var email = NewEmail();
        var enrol = await InviteAsync(business.Admin, StaffInvite(business, email));
        var original = (await (await RedeemAsync(anonymous, enrol.Token)).Content.ReadFromJsonAsync<Tokens>(JsonOptions))!;
        var staff = (await business.Admin.GetFromJsonAsync<List<MemberDto>>("/staff/members", JsonOptions))!.Single(m => m.Email == email);

        var resetResponse = await business.Admin.PostAsync($"/staff/members/{staff.Id}/reset-link", null);
        var reset = (await resetResponse.Content.ReadFromJsonAsync<InviteLinkDto>(JsonOptions))!;
        Assert.Equal(InvitePurpose.PasswordReset, reset.Invite.Purpose);

        const string newPassword = "a brand new passphrase";
        var done = await anonymous.PostAsJsonAsync("/enrol/redeem", new RedeemInviteRequest(reset.Token, newPassword, "903412"));
        Assert.Equal(HttpStatusCode.OK, done.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/auth/sign-in", new SignInRequest(email, Password))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.PostAsJsonAsync("/auth/sign-in", new SignInRequest(email, newPassword))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest(original.RefreshToken))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.PostAsJsonAsync("/enrol/redeem", new RedeemInviteRequest(reset.Token, newPassword, null))).StatusCode);
    }

    [Fact]
    public async Task A_business_cannot_reset_the_password_of_a_login_that_is_also_used_at_another_business()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var first = await NewBusinessAsync(factory);
        var second = await NewBusinessAsync(factory);
        using var anonymous = factory.CreateClient();
        var email = NewEmail();
        Assert.Equal(HttpStatusCode.OK, (await RedeemAsync(anonymous, (await InviteAsync(first.Admin, StaffInvite(first, email))).Token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await RedeemAsync(anonymous, (await InviteAsync(second.Admin, StaffInvite(second, email, StaffDuty.Warehouse))).Token)).StatusCode);
        var inFirst = (await first.Admin.GetFromJsonAsync<List<MemberDto>>("/staff/members", JsonOptions))!.Single(m => m.Email == email);

        // The first business's admin must not be able to set a password that also opens the second business.
        var reset = await first.Admin.PostAsync($"/staff/members/{inFirst.Id}/reset-link", null);

        Assert.Equal(HttpStatusCode.Forbidden, reset.StatusCode);
        Assert.Contains("chooseBusiness", await (await anonymous.PostAsJsonAsync("/auth/sign-in", new SignInRequest(email, Password))).Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Staff_cannot_invite_anyone()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var business = await NewBusinessAsync(factory);
        using var anonymous = factory.CreateClient();
        var link = await InviteAsync(business.Admin, StaffInvite(business));
        var tokens = (await (await RedeemAsync(anonymous, link.Token)).Content.ReadFromJsonAsync<Tokens>(JsonOptions))!;

        var response = await As(factory, tokens.AccessToken).PostAsJsonAsync("/staff/invites", StaffInvite(business));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/staff/invites", StaffInvite(business))).StatusCode);
    }
}
