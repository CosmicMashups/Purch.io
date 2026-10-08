using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Purch.Application.Auth;
using Purch.Application.Devices;
using Purch.Application.Onboarding;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests;

/// <summary>What each kind of session can reach. A Cashier on a Register gets the till and nothing else; a Warehouse device gets
/// Inventory and nothing else; only Admin and Manager reach the dashboard's revenue and the staff list; only an Admin
/// reaches Devices; and an unattended device (kiosk, displays) reaches none of the staff pages.</summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class AccessMatrixTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string Password = "correct horse battery";

    private sealed record Tokens(string AccessToken, string RefreshToken);

    private sealed record Unlocked(string AccessToken, string RefreshToken);

    /// <summary>One representative endpoint per area. The status says whether the area is open to the session.</summary>
    private static readonly (string Area, string Path)[] Areas =
    [
        ("till", "/transactions/cart"),
        ("inventory", "/inventory/dashboard"),
        ("revenue dashboard", "/reports/sales-dashboard"),
        ("staff list", "/staff/members"),
        ("devices", "/devices"),
    ];

    private static HttpClient Bearer(PurchApiFactory factory, string accessToken)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private static async Task<Dictionary<string, bool>> OpenAreasAsync(HttpClient client)
    {
        var open = new Dictionary<string, bool>();
        foreach (var (area, path) in Areas)
        {
            var status = (await client.GetAsync(path)).StatusCode;
            Assert.True(status is HttpStatusCode.OK or HttpStatusCode.Forbidden, $"{area}: unexpected {status}");
            open[area] = status == HttpStatusCode.OK;
        }

        return open;
    }

    private async Task<(PurchApiFactory Factory, HttpClient Admin, BootstrapTenantResult Business, HttpClient Anonymous)> NewShopAsync()
    {
        var factory = new PurchApiFactory(postgres.ConnectionString);
        var anonymous = factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@example.com";
        var bootstrap = await anonymous.PostAsJsonAsync(
            "/onboarding/bootstrap",
            new BootstrapTenantRequest($"Store {Guid.NewGuid():N}", BusinessType.ConvenienceStore, "Main", "Ana Reyes", "123412", email, Password));
        var business = (await bootstrap.Content.ReadFromJsonAsync<BootstrapTenantResult>(JsonOptions))!;
        var tokens = (await (await anonymous.PostAsJsonAsync("/auth/sign-in", new SignInRequest(email, Password, business.TenantId))).Content.ReadFromJsonAsync<Tokens>(JsonOptions))!;
        return (factory, Bearer(factory, tokens.AccessToken), business, anonymous);
    }

    private static async Task<(Guid MembershipId, string Pin)> AddPersonAsync(HttpClient admin, HttpClient anonymous, BootstrapTenantResult business, MembershipRole role, StaffDuty duties)
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        var branches = role == MembershipRole.Staff ? new[] { business.BranchId } : null;
        var invite = (await (await admin.PostAsJsonAsync("/staff/invites", new CreateInviteRequest("Person", email, role, duties, branches))).Content.ReadFromJsonAsync<InviteLinkDto>(JsonOptions))!;
        _ = await anonymous.PostAsJsonAsync("/enrol/redeem", new RedeemInviteRequest(invite.Token, Password, "482112"));
        var members = (await admin.GetFromJsonAsync<List<MemberDto>>("/staff/members", JsonOptions))!;
        return (members.Single(m => m.Email == email).Id, "482112");
    }

    private static async Task<string> PairAsync(HttpClient admin, HttpClient anonymous, BootstrapTenantResult business, DeviceType type)
    {
        var request = (await (await admin.PostAsJsonAsync("/devices/pairing-requests", new CreateDevicePairingRequest("Device", type, business.BranchId, type == DeviceType.CustomerDisplay ? await RegisterIdAsync(admin, anonymous, business) : null))).Content.ReadFromJsonAsync<DevicePairingCodeDto>(JsonOptions))!;
        return (await (await anonymous.PostAsJsonAsync("/devices/pair", new PairDeviceRequest(request.PairingCode))).Content.ReadFromJsonAsync<PairedDeviceDto>(JsonOptions))!.DeviceCredential;
    }

    private static async Task<Guid> RegisterIdAsync(HttpClient admin, HttpClient anonymous, BootstrapTenantResult business)
    {
        var request = (await (await admin.PostAsJsonAsync("/devices/pairing-requests", new CreateDevicePairingRequest("Till", DeviceType.Register, business.BranchId))).Content.ReadFromJsonAsync<DevicePairingCodeDto>(JsonOptions))!;
        _ = await anonymous.PostAsJsonAsync("/devices/pair", new PairDeviceRequest(request.PairingCode));
        return request.Device.Id;
    }

    private static async Task<string> UnlockAsync(HttpClient anonymous, string credential, (Guid MembershipId, string Pin) person)
    {
        var response = await anonymous.PostAsJsonAsync("/devices/unlock", new UnlockRequest(credential, person.MembershipId, person.Pin));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Unlocked>(JsonOptions))!.AccessToken;
    }

    [Fact]
    public async Task A_cashier_on_a_register_reaches_the_till_and_nothing_else()
    {
        var (factory, admin, business, anonymous) = await NewShopAsync();
        await using var _ = factory;
        var cashier = await AddPersonAsync(admin, anonymous, business, MembershipRole.Staff, StaffDuty.Cashier);
        var token = await UnlockAsync(anonymous, await PairAsync(admin, anonymous, business, DeviceType.Register), cashier);

        var open = await OpenAreasAsync(Bearer(factory, token));

        Assert.Equal(["till"], open.Where(kv => kv.Value).Select(kv => kv.Key).ToList());
    }

    [Fact]
    public async Task A_warehouse_device_reaches_inventory_and_nothing_else()
    {
        var (factory, admin, business, anonymous) = await NewShopAsync();
        await using var _ = factory;
        var warehouse = await AddPersonAsync(admin, anonymous, business, MembershipRole.Staff, StaffDuty.Warehouse);
        var token = await UnlockAsync(anonymous, await PairAsync(admin, anonymous, business, DeviceType.WarehouseOfficer), warehouse);

        var open = await OpenAreasAsync(Bearer(factory, token));

        Assert.Equal(["inventory"], open.Where(kv => kv.Value).Select(kv => kv.Key).ToList());
    }

    [Fact]
    public async Task A_manager_reaches_everything_except_devices_wherever_they_unlock()
    {
        var (factory, admin, business, anonymous) = await NewShopAsync();
        await using var _ = factory;
        var manager = await AddPersonAsync(admin, anonymous, business, MembershipRole.Manager, StaffDuty.None);
        var onRegister = await UnlockAsync(anonymous, await PairAsync(admin, anonymous, business, DeviceType.Register), manager);
        var onWarehouse = await UnlockAsync(anonymous, await PairAsync(admin, anonymous, business, DeviceType.WarehouseOfficer), manager);

        foreach (var token in new[] { onRegister, onWarehouse })
        {
            var open = await OpenAreasAsync(Bearer(factory, token));
            Assert.Equal(["till", "inventory", "revenue dashboard", "staff list"], open.Where(kv => kv.Value).Select(kv => kv.Key).ToList());
        }
    }

    [Fact]
    public async Task An_admin_reaches_every_area()
    {
        var (factory, admin, _, _) = await NewShopAsync();
        await using var _ = factory;

        var open = await OpenAreasAsync(admin);

        // The till itself needs a paired device, which an email sign-in on a personal device does not have.
        Assert.True(open["inventory"] && open["revenue dashboard"] && open["staff list"] && open["devices"]);
    }

    [Theory]
    [InlineData(DeviceType.Kiosk)]
    [InlineData(DeviceType.OrderBoard)]
    [InlineData(DeviceType.KitchenDisplay)]
    [InlineData(DeviceType.CustomerDisplay)]
    public async Task An_unattended_device_reaches_none_of_the_staff_pages(DeviceType type)
    {
        var (factory, admin, business, anonymous) = await NewShopAsync();
        await using var _ = factory;
        var credential = await PairAsync(admin, anonymous, business, type);
        var session = (await (await anonymous.PostAsJsonAsync("/devices/session", new DeviceSessionRequest(credential))).Content.ReadFromJsonAsync<DeviceSessionDto>(JsonOptions))!;

        var open = await OpenAreasAsync(Bearer(factory, session.AccessToken!));

        Assert.DoesNotContain(open.Values, isOpen => isOpen);
    }

    [Fact]
    public async Task A_staff_member_on_a_personal_device_with_only_cashier_duty_cannot_reach_inventory_or_reports()
    {
        var (factory, admin, business, anonymous) = await NewShopAsync();
        await using var _ = factory;
        var email = $"{Guid.NewGuid():N}@example.com";
        var invite = (await (await admin.PostAsJsonAsync("/staff/invites", new CreateInviteRequest("Ben", email, MembershipRole.Staff, StaffDuty.Cashier, [business.BranchId]))).Content.ReadFromJsonAsync<InviteLinkDto>(JsonOptions))!;
        var tokens = (await (await anonymous.PostAsJsonAsync("/enrol/redeem", new RedeemInviteRequest(invite.Token, Password, "482112"))).Content.ReadFromJsonAsync<Tokens>(JsonOptions))!;

        var open = await OpenAreasAsync(Bearer(factory, tokens.AccessToken));

        Assert.False(open["inventory"] || open["revenue dashboard"] || open["staff list"] || open["devices"]);
    }
}
