using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Purch.Application.Auth;
using Purch.Application.Devices;
using Purch.Application.Onboarding;
using Purch.Domain.Enums;

namespace Purch.IntegrationTests.Fixtures;

/// <summary>Signs people in the way the product does, for tests that need a session: register a business, pair a Register, invite
/// staff and unlock the till with their PIN. The old shortcut (a pairing code and a PIN straight to a token) is gone.</summary>
public static class TestSessions
{
    public const string Password = "correct horse battery";

    public const string AdminPin = "123412";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>One business with its paired Register: what a test needs to sign more people in.</summary>
    public sealed record Shop(PurchApiFactory Factory, BootstrapTenantResult Tenant, string AdminEmail, Guid RegisterId, string RegisterCredential);

    private sealed record Tokens(string AccessToken, string RefreshToken);

    private sealed record Unlocked(string AccessToken, string RefreshToken);

    // The admin client is what tests hold on to, so the shop it belongs to is found from it.
    private static readonly ConditionalWeakTable<HttpClient, Shop> ShopByAdmin = [];

    public static Shop ShopOf(HttpClient adminClient) => ShopByAdmin.TryGetValue(adminClient, out var shop)
        ? shop
        : throw new InvalidOperationException("This client was not created by TestSessions.AdminClientAsync.");

    public static HttpClient Bearer(PurchApiFactory factory, string accessToken)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    /// <summary>A new business whose owner has unlocked its Register: a session with the Admin role that is also tied to a
    /// device, so it can sell. The stand-in for the old "bootstrap, then log in with the pairing code and PIN".</summary>
    public static async Task<HttpClient> AdminClientAsync(PurchApiFactory factory, string? businessName = null, BusinessType businessType = BusinessType.ConvenienceStore)
    {
        using var anonymous = factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@example.com";
        var bootstrap = await anonymous.PostAsJsonAsync(
            "/onboarding/bootstrap",
            new BootstrapTenantRequest(businessName ?? $"Tenant-{Guid.NewGuid():N}", businessType, "Main Branch", "Admin User", AdminPin, email, Password));
        bootstrap.EnsureSuccessStatusCode();
        var tenant = (await bootstrap.Content.ReadFromJsonAsync<BootstrapTenantResult>(JsonOptions))!;

        var signedIn = (await (await anonymous.PostAsJsonAsync("/auth/sign-in", new SignInRequest(email, Password, tenant.TenantId))).Content.ReadFromJsonAsync<Tokens>(JsonOptions))!;
        using var personal = Bearer(factory, signedIn.AccessToken);
        var (registerId, credential) = await PairAsync(personal, anonymous, tenant.BranchId, DeviceType.Register, "Till");

        var unlock = await anonymous.PostAsJsonAsync("/devices/unlock", new UnlockRequest(credential, tenant.AdminMembershipId, AdminPin));
        unlock.EnsureSuccessStatusCode();
        var admin = Bearer(factory, (await unlock.Content.ReadFromJsonAsync<Unlocked>(JsonOptions))!.AccessToken);
        ShopByAdmin.Add(admin, new Shop(factory, tenant, email, registerId, credential));
        return admin;
    }

    public static async Task<(Guid DeviceId, string Credential)> PairAsync(HttpClient admin, HttpClient anonymous, Guid branchId, DeviceType type, string name, Guid? linkedRegisterId = null)
    {
        var request = await admin.PostAsJsonAsync("/devices/pairing-requests", new CreateDevicePairingRequest(name, type, branchId, linkedRegisterId));
        request.EnsureSuccessStatusCode();
        var code = (await request.Content.ReadFromJsonAsync<DevicePairingCodeDto>(JsonOptions))!;
        var paired = await anonymous.PostAsJsonAsync("/devices/pair", new PairDeviceRequest(code.PairingCode));
        paired.EnsureSuccessStatusCode();
        var device = (await paired.Content.ReadFromJsonAsync<PairedDeviceDto>(JsonOptions))!;
        return (device.DeviceId, device.DeviceCredential);
    }

    /// <summary>Invites a person, has them open the link, and returns their membership id.</summary>
    public static async Task<Guid> AddPersonAsync(HttpClient admin, string name, MembershipRole role, StaffDuty duties, string pin, Guid? branchId = null)
    {
        var shop = ShopOf(admin);
        var email = $"{Guid.NewGuid():N}@example.com";
        var branches = role == MembershipRole.Staff ? new[] { branchId ?? shop.Tenant.BranchId } : null;
        var invited = await admin.PostAsJsonAsync("/staff/invites", new CreateInviteRequest(name, email, role, duties, branches));
        invited.EnsureSuccessStatusCode();
        var link = (await invited.Content.ReadFromJsonAsync<InviteLinkDto>(JsonOptions))!;

        using var anonymous = shop.Factory.CreateClient();
        (await anonymous.PostAsJsonAsync("/enrol/redeem", new RedeemInviteRequest(link.Token, Password, pin))).EnsureSuccessStatusCode();
        var members = (await admin.GetFromJsonAsync<List<MemberDto>>("/staff/members", JsonOptions))!;
        return members.Single(m => m.Email == email).Id;
    }

    /// <summary>A person signed in on a paired device by typing their PIN. The business's own Register unless another is given.</summary>
    public static async Task<HttpClient> UnlockAsync(HttpClient admin, Guid membershipId, string pin, string? credential = null)
    {
        var shop = ShopOf(admin);
        using var anonymous = shop.Factory.CreateClient();
        var unlock = await anonymous.PostAsJsonAsync("/devices/unlock", new UnlockRequest(credential ?? shop.RegisterCredential, membershipId, pin));
        unlock.EnsureSuccessStatusCode();
        return Bearer(shop.Factory, (await unlock.Content.ReadFromJsonAsync<Unlocked>(JsonOptions))!.AccessToken);
    }

    /// <summary>What the till is handed when this person unlocks it: the supervisor attestation, or null for staff who are not
    /// a manager or admin. Offline discount sales carry it.</summary>
    public static async Task<string?> SupervisorAttestationAsync(HttpClient admin, Guid membershipId, string pin, string? credential = null)
    {
        var shop = ShopOf(admin);
        using var anonymous = shop.Factory.CreateClient();
        var unlock = await anonymous.PostAsJsonAsync("/devices/unlock", new UnlockRequest(credential ?? shop.RegisterCredential, membershipId, pin));
        unlock.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await unlock.Content.ReadAsStringAsync());
        return body.RootElement.TryGetProperty("supervisorAttestation", out var token) && token.ValueKind == JsonValueKind.String ? token.GetString() : null;
    }

    /// <summary>The owner signed in on a second Register, as a second terminal in the same branch.</summary>
    public static async Task<HttpClient> AdminOnNewRegisterAsync(HttpClient admin, string name = "Second Terminal")
    {
        var shop = ShopOf(admin);
        using var anonymous = shop.Factory.CreateClient();
        var (_, credential) = await PairAsync(admin, anonymous, shop.Tenant.BranchId, DeviceType.Register, name);
        return await UnlockAsync(admin, shop.Tenant.AdminMembershipId, AdminPin, credential);
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, string> WarehouseCredentials = new();

    /// <summary>The shop's Warehouse device, paired the first time it is needed: warehouse-only staff cannot unlock a Register.</summary>
    public static async Task<string> WarehouseCredentialAsync(HttpClient admin)
    {
        var shop = ShopOf(admin);
        if (WarehouseCredentials.TryGetValue(shop.Tenant.TenantId, out var existing))
        {
            return existing;
        }

        using var anonymous = shop.Factory.CreateClient();
        var (_, credential) = await PairAsync(admin, anonymous, shop.Tenant.BranchId, DeviceType.WarehouseOfficer, "Stock room");
        WarehouseCredentials[shop.Tenant.TenantId] = credential;
        return credential;
    }

    /// <summary>Invite, enrol and unlock in one go: staff on the device their duty belongs to (the shop's Register, or its Warehouse
    /// device for someone who only does warehouse work).</summary>
    public static async Task<HttpClient> StaffClientAsync(HttpClient admin, string name, MembershipRole role, StaffDuty duties, string pin, Guid? branchId = null)
    {
        var id = await AddPersonAsync(admin, name, role, duties, pin, branchId);
        var warehouseOnly = role == MembershipRole.Staff && !duties.HasFlag(StaffDuty.Cashier) && duties.HasFlag(StaffDuty.Warehouse);
        return await UnlockAsync(admin, id, pin, warehouseOnly ? await WarehouseCredentialAsync(admin) : null);
    }

    public static Task<HttpClient> CashierClientAsync(HttpClient admin, string pin = "678912", string name = "Cal Cashier")
        => StaffClientAsync(admin, name, MembershipRole.Staff, StaffDuty.Cashier, pin);

    public static Task<HttpClient> ManagerClientAsync(HttpClient admin, string pin = "567812", string name = "Mae Manager")
        => StaffClientAsync(admin, name, MembershipRole.Manager, StaffDuty.None, pin);
}
