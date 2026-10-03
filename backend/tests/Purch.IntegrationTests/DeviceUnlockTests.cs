using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Purch.Api.Endpoints;
using Purch.Application.Auth;
using Purch.Application.Devices;
using Purch.Application.Onboarding;
using Purch.Common.TestUtilities;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;
using Purch.Infrastructure.Persistence;

namespace Purch.IntegrationTests;

/// <summary>A person picks their name on a paired till and types their own PIN. The PIN is checked against that one person,
/// a few misses lock only them out, and the device's duty (not the person's account) decides what they are on it.</summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class DeviceUnlockTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string Password = "correct horse battery";

    private sealed record Tokens(string AccessToken, string RefreshToken);

    private sealed record Unlocked(string AccessToken, string RefreshToken, RosterEntryDto Person);

    private sealed record Person(Guid MembershipId, string Email, string Pin);

    private sealed class Shop(PurchApiFactory factory, HttpClient admin, BootstrapTenantResult business)
    {
        public PurchApiFactory Factory { get; } = factory;

        public HttpClient Admin { get; } = admin;

        public BootstrapTenantResult Business { get; } = business;

        public HttpClient Anonymous { get; } = factory.CreateClient();
    }

    private static async Task<Shop> NewShopAsync(PurchApiFactory factory)
    {
        using var anonymous = factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@example.com";
        var bootstrap = await anonymous.PostAsJsonAsync(
            "/onboarding/bootstrap",
            new BootstrapTenantRequest($"Store {Guid.NewGuid():N}", BusinessType.ConvenienceStore, "Main", "Ana Reyes", "1234", email, Password));
        var business = (await bootstrap.Content.ReadFromJsonAsync<BootstrapTenantResult>(JsonOptions))!;
        var tokens = (await (await anonymous.PostAsJsonAsync("/auth/sign-in", new SignInRequest(email, Password, business.TenantId))).Content.ReadFromJsonAsync<Tokens>(JsonOptions))!;
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return new Shop(factory, admin, business);
    }

    private static async Task<Person> AddPersonAsync(Shop shop, string name, MembershipRole role, StaffDuty duties, string pin, Guid? branchId = null)
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        var branches = role == MembershipRole.Staff ? new[] { branchId ?? shop.Business.BranchId } : null;
        var invite = (await (await shop.Admin.PostAsJsonAsync("/staff/invites", new CreateInviteRequest(name, email, role, duties, branches))).Content.ReadFromJsonAsync<InviteLinkDto>(JsonOptions))!;
        var redeemed = await shop.Anonymous.PostAsJsonAsync("/enrol/redeem", new RedeemInviteRequest(invite.Token, Password, pin));
        Assert.Equal(HttpStatusCode.OK, redeemed.StatusCode);
        var members = (await shop.Admin.GetFromJsonAsync<List<MemberDto>>("/staff/members", JsonOptions))!;
        return new Person(members.Single(m => m.Email == email).Id, email, pin);
    }

    /// <summary>Creates a device of the type, pairs it, and returns its credential.</summary>
    private static async Task<(Guid DeviceId, string Credential)> PairAsync(Shop shop, DeviceType type)
    {
        var request = (await (await shop.Admin.PostAsJsonAsync("/devices/pairing-requests", new CreateDevicePairingRequest("Device", type, shop.Business.BranchId))).Content.ReadFromJsonAsync<DevicePairingCodeDto>(JsonOptions))!;
        var paired = (await (await shop.Anonymous.PostAsJsonAsync("/devices/pair", new PairDeviceRequest(request.PairingCode))).Content.ReadFromJsonAsync<PairedDeviceDto>(JsonOptions))!;
        return (paired.DeviceId, paired.DeviceCredential);
    }

    private static Task<HttpResponseMessage> UnlockAsync(Shop shop, string credential, Person person, string? pin = null)
        => shop.Anonymous.PostAsJsonAsync("/devices/unlock", new UnlockRequest(credential, person.MembershipId, pin ?? person.Pin));

    private static async Task<DeviceRosterDto> RosterAsync(Shop shop, string credential)
    {
        var response = await shop.Anonymous.PostAsJsonAsync("/devices/roster", new RosterRequest(credential));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<DeviceRosterDto>(JsonOptions))!;
    }

    private static JwtSecurityToken Read(string accessToken) => new JwtSecurityTokenHandler().ReadJwtToken(accessToken);

    private PurchDbContext NewContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<PurchDbContext>().UseNpgsql(postgres.ConnectionString).Options;
        return new PurchDbContext(options, new TestCurrentTenantProvider { TenantId = tenantId });
    }

    [Fact]
    public async Task The_roster_lists_only_people_who_may_work_on_that_device()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var shop = await NewShopAsync(factory);
        var cashier = await AddPersonAsync(shop, "Ben Santos", MembershipRole.Staff, StaffDuty.Cashier, "4821");
        _ = await AddPersonAsync(shop, "Wally Cruz", MembershipRole.Staff, StaffDuty.Warehouse, "5532");
        _ = await AddPersonAsync(shop, "Manny Lopez", MembershipRole.Manager, StaffDuty.None, "6643");
        var otherBranch = (await (await shop.Admin.PostAsJsonAsync("/branches", new CreateBranchRequest("Other branch", null))).Content.ReadFromJsonAsync<BranchDto>(JsonOptions))!;
        _ = await AddPersonAsync(shop, "Faraway Fay", MembershipRole.Staff, StaffDuty.Cashier, "7754", otherBranch.Id);
        var inactive = await AddPersonAsync(shop, "Gone Gina", MembershipRole.Staff, StaffDuty.Cashier, "8865");
        _ = await shop.Admin.PutAsJsonAsync($"/staff/members/{inactive.MembershipId}", new UpdateMemberRequest(MembershipRole.Staff, StaffDuty.Cashier, [shop.Business.BranchId], false));
        var (_, credential) = await PairAsync(shop, DeviceType.Register);

        var roster = await RosterAsync(shop, credential);

        Assert.Equal(DeviceType.Register, roster.DeviceType);
        Assert.Equal(["Ana Reyes", "Ben Santos", "Manny Lopez"], roster.People.Select(p => p.Name).Order().ToList());
        Assert.Contains(roster.People, p => p.MembershipId == cashier.MembershipId && p.HasPin);

        var (_, warehouseCredential) = await PairAsync(shop, DeviceType.WarehouseOfficer);
        Assert.Contains("Wally Cruz", (await RosterAsync(shop, warehouseCredential)).People.Select(p => p.Name));
        Assert.DoesNotContain("Ben Santos", (await RosterAsync(shop, warehouseCredential)).People.Select(p => p.Name));
    }

    [Fact]
    public async Task Only_a_register_or_warehouse_device_with_a_live_credential_has_a_roster()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var shop = await NewShopAsync(factory);
        var (kioskId, kioskCredential) = await PairAsync(shop, DeviceType.Kiosk);
        var (registerId, registerCredential) = await PairAsync(shop, DeviceType.Register);

        Assert.Equal(HttpStatusCode.Unauthorized, (await shop.Anonymous.PostAsJsonAsync("/devices/roster", new RosterRequest(kioskCredential))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await shop.Anonymous.PostAsJsonAsync("/devices/roster", new RosterRequest("made-up"))).StatusCode);
        Assert.NotEqual(kioskId, registerId);

        _ = await shop.Admin.PostAsync($"/devices/{registerId}/revoke", null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await shop.Anonymous.PostAsJsonAsync("/devices/roster", new RosterRequest(registerCredential))).StatusCode);
    }

    [Fact]
    public async Task A_cashier_unlocks_a_register_with_their_pin_and_is_a_cashier_there()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var shop = await NewShopAsync(factory);
        var cashier = await AddPersonAsync(shop, "Ben Santos", MembershipRole.Staff, StaffDuty.Cashier, "4821");
        var (deviceId, credential) = await PairAsync(shop, DeviceType.Register);

        var response = await UnlockAsync(shop, credential, cashier);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var unlocked = (await response.Content.ReadFromJsonAsync<Unlocked>(JsonOptions))!;
        var jwt = Read(unlocked.AccessToken);
        Assert.Equal(nameof(Role.Cashier), jwt.Claims.Single(c => c.Type == JwtClaimTypes.Role).Value);
        Assert.Equal(deviceId.ToString(), jwt.Claims.Single(c => c.Type == JwtClaimTypes.DeviceId).Value);
        Assert.Equal(shop.Business.BranchId.ToString(), jwt.Claims.Single(c => c.Type == JwtClaimTypes.BranchId).Value);
        Assert.Equal(cashier.MembershipId.ToString(), jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Sub).Value);

        // The session reaches a till endpoint, and renewing it keeps it on this device.
        using var till = factory.CreateClient();
        till.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", unlocked.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await till.GetAsync("/transactions/cart")).StatusCode);

        var renewed = await shop.Anonymous.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest(unlocked.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, renewed.StatusCode);
        var renewedJwt = Read((await renewed.Content.ReadFromJsonAsync<Tokens>(JsonOptions))!.AccessToken);
        Assert.Equal(deviceId.ToString(), renewedJwt.Claims.Single(c => c.Type == JwtClaimTypes.DeviceId).Value);
        Assert.Equal(nameof(Role.Cashier), renewedJwt.Claims.Single(c => c.Type == JwtClaimTypes.Role).Value);
    }

    [Fact]
    public async Task The_device_decides_the_role_and_a_person_who_does_not_fit_it_cannot_sign_in()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var shop = await NewShopAsync(factory);
        var warehouse = await AddPersonAsync(shop, "Wally Cruz", MembershipRole.Staff, StaffDuty.Warehouse, "5532");
        var allRounder = await AddPersonAsync(shop, "Rhea Dela Cruz", MembershipRole.Staff, StaffDuty.Cashier | StaffDuty.Warehouse, "9976");
        var manager = await AddPersonAsync(shop, "Manny Lopez", MembershipRole.Manager, StaffDuty.None, "6643");
        var (_, register) = await PairAsync(shop, DeviceType.Register);
        var (_, warehouseDevice) = await PairAsync(shop, DeviceType.WarehouseOfficer);

        Assert.Equal(HttpStatusCode.Unauthorized, (await UnlockAsync(shop, register, warehouse)).StatusCode);

        string RoleOf(HttpResponseMessage r) => Read(r.Content.ReadFromJsonAsync<Unlocked>(JsonOptions).Result!.AccessToken).Claims.Single(c => c.Type == JwtClaimTypes.Role).Value;
        Assert.Equal(nameof(Role.Warehouse), RoleOf(await UnlockAsync(shop, warehouseDevice, warehouse)));
        Assert.Equal(nameof(Role.Cashier), RoleOf(await UnlockAsync(shop, register, allRounder)));
        Assert.Equal(nameof(Role.Warehouse), RoleOf(await UnlockAsync(shop, warehouseDevice, allRounder)));
        Assert.Equal(nameof(Role.Manager), RoleOf(await UnlockAsync(shop, register, manager)));
    }

    [Fact]
    public async Task Wrong_pins_count_against_that_person_only_and_lock_them_out_after_five()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var shop = await NewShopAsync(factory);
        var ben = await AddPersonAsync(shop, "Ben Santos", MembershipRole.Staff, StaffDuty.Cashier, "4821");
        var cara = await AddPersonAsync(shop, "Cara Uy", MembershipRole.Staff, StaffDuty.Cashier, "3309");
        var (_, credential) = await PairAsync(shop, DeviceType.Register);

        for (var left = 4; left >= 1; left--)
        {
            var miss = await UnlockAsync(shop, credential, ben, "0000");
            Assert.Equal(HttpStatusCode.Unauthorized, miss.StatusCode);
            Assert.Contains($"\"attemptsLeft\":{left}", await miss.Content.ReadAsStringAsync());
        }

        var locked = await UnlockAsync(shop, credential, ben, "0000");
        Assert.Equal(HttpStatusCode.TooManyRequests, locked.StatusCode);
        Assert.Contains("lockedUntil", await locked.Content.ReadAsStringAsync());

        // Even the right PIN is refused while locked, but someone else is unaffected.
        Assert.Equal(HttpStatusCode.TooManyRequests, (await UnlockAsync(shop, credential, ben)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await UnlockAsync(shop, credential, cara)).StatusCode);

        // When the lock runs out the right PIN works and the count starts over.
        await using (var db = NewContext(shop.Business.TenantId))
        {
            var row = await db.Memberships.SingleAsync(m => m.Id == ben.MembershipId);
            row.PinLockedUntil = DateTimeOffset.UtcNow.AddMinutes(-1);
            _ = await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.OK, (await UnlockAsync(shop, credential, ben)).StatusCode);
        await using var check = NewContext(shop.Business.TenantId);
        Assert.Equal(0, (await check.Memberships.SingleAsync(m => m.Id == ben.MembershipId)).PinFailedAttempts);
    }

    [Fact]
    public async Task Revoking_the_device_ends_an_unlocked_session()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var shop = await NewShopAsync(factory);
        var ben = await AddPersonAsync(shop, "Ben Santos", MembershipRole.Staff, StaffDuty.Cashier, "4821");
        var (deviceId, credential) = await PairAsync(shop, DeviceType.Register);
        var unlocked = (await (await UnlockAsync(shop, credential, ben)).Content.ReadFromJsonAsync<Unlocked>(JsonOptions))!;
        using var till = factory.CreateClient();
        till.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", unlocked.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await till.GetAsync("/transactions/cart")).StatusCode);

        _ = await shop.Admin.PostAsync($"/devices/{deviceId}/revoke", null);

        Assert.Equal(HttpStatusCode.Unauthorized, (await till.GetAsync("/transactions/cart")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await shop.Anonymous.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest(unlocked.RefreshToken))).StatusCode);
    }

    [Fact]
    public async Task A_managers_pin_is_known_to_the_approval_checks_and_a_person_is_shown_to_older_lookups_as_a_user()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var shop = await NewShopAsync(factory);
        var manager = await AddPersonAsync(shop, "Manny Lopez", MembershipRole.Manager, StaffDuty.None, "6643");
        var cashier = await AddPersonAsync(shop, "Ben Santos", MembershipRole.Staff, StaffDuty.Cashier, "4821");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var approvers = (await users.GetActiveActorsAsync(shop.Business.TenantId)).Where(u => u.Role is Role.Admin or Role.Manager).ToList();
            var projected = approvers.Single(u => u.Id == manager.MembershipId);
            Assert.Equal("Manny Lopez", projected.Name);
            Assert.True(BCrypt.Net.BCrypt.Verify("6643", projected.PinHash));

            var everyone = await users.ListActorsAsync(shop.Business.TenantId);
            var projectedCashier = everyone.Single(u => u.Id == cashier.MembershipId);
            Assert.Equal("Ben Santos", projectedCashier.Name);
            Assert.Equal(Role.Cashier, projectedCashier.Role);
        }
    }
}
