using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Purch.Application.Auth;
using Purch.Application.Devices;
using Purch.Application.Onboarding;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests;

/// <summary>A customer display paired to a Register follows that Register's cart through the server: the Register pushes, the
/// display polls with a conditional GET. A display only ever sees its own Register.</summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class CustomerDisplayTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string Password = "correct horse battery";

    private sealed record Tokens(string AccessToken, string RefreshToken);

    private sealed record Unlocked(string AccessToken, string RefreshToken);

    private sealed record Feed(long Version, JsonElement? State);

    private sealed class Shop(PurchApiFactory factory, HttpClient admin, BootstrapTenantResult business, HttpClient anonymous)
    {
        public PurchApiFactory Factory { get; } = factory;

        public HttpClient Admin { get; } = admin;

        public BootstrapTenantResult Business { get; } = business;

        public HttpClient Anonymous { get; } = anonymous;
    }

    private sealed record Till(Guid DeviceId, HttpClient Client);

    private sealed record Screen(Guid DeviceId, HttpClient Client);

    private static async Task<Shop> NewShopAsync(PurchApiFactory factory)
    {
        var anonymous = factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@example.com";
        var bootstrap = await anonymous.PostAsJsonAsync(
            "/onboarding/bootstrap",
            new BootstrapTenantRequest($"Store {Guid.NewGuid():N}", BusinessType.ConvenienceStore, "Main", "Ana Reyes", "123412", email, Password));
        var business = (await bootstrap.Content.ReadFromJsonAsync<BootstrapTenantResult>(JsonOptions))!;
        var tokens = (await (await anonymous.PostAsJsonAsync("/auth/sign-in", new SignInRequest(email, Password, business.TenantId))).Content.ReadFromJsonAsync<Tokens>(JsonOptions))!;
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return new Shop(factory, admin, business, anonymous);
    }

    private static async Task<(Guid DeviceId, string Credential)> PairAsync(Shop shop, DeviceType type, string name, Guid? linkedRegister = null)
    {
        var request = (await (await shop.Admin.PostAsJsonAsync("/devices/pairing-requests", new CreateDevicePairingRequest(name, type, shop.Business.BranchId, linkedRegister))).Content.ReadFromJsonAsync<DevicePairingCodeDto>(JsonOptions))!;
        var paired = (await (await shop.Anonymous.PostAsJsonAsync("/devices/pair", new PairDeviceRequest(request.PairingCode))).Content.ReadFromJsonAsync<PairedDeviceDto>(JsonOptions))!;
        return (paired.DeviceId, paired.DeviceCredential);
    }

    /// <summary>A Register with a cashier signed in on it.</summary>
    private static async Task<Till> NewTillAsync(Shop shop, string name = "Till")
    {
        var (deviceId, credential) = await PairAsync(shop, DeviceType.Register, name);
        var email = $"{Guid.NewGuid():N}@example.com";
        var invite = (await (await shop.Admin.PostAsJsonAsync("/staff/invites", new CreateInviteRequest("Ben", email, MembershipRole.Staff, StaffDuty.Cashier, [shop.Business.BranchId]))).Content.ReadFromJsonAsync<InviteLinkDto>(JsonOptions))!;
        _ = await shop.Anonymous.PostAsJsonAsync("/enrol/redeem", new RedeemInviteRequest(invite.Token, Password, "482112"));
        var members = (await shop.Admin.GetFromJsonAsync<List<MemberDto>>("/staff/members", JsonOptions))!;
        var unlocked = (await (await shop.Anonymous.PostAsJsonAsync("/devices/unlock", new UnlockRequest(credential, members.Single(m => m.Email == email).Id, "482112"))).Content.ReadFromJsonAsync<Unlocked>(JsonOptions))!;
        var client = shop.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", unlocked.AccessToken);
        return new Till(deviceId, client);
    }

    private static async Task<Screen> NewScreenAsync(Shop shop, Till till)
    {
        var (deviceId, credential) = await PairAsync(shop, DeviceType.CustomerDisplay, "Screen", till.DeviceId);
        var session = (await (await shop.Anonymous.PostAsJsonAsync("/devices/session", new DeviceSessionRequest(credential))).Content.ReadFromJsonAsync<DeviceSessionDto>(JsonOptions))!;
        var client = shop.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return new Screen(deviceId, client);
    }

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    private const string Cart = """{"mode":"cart","lines":[{"name":"Latte","quantity":2,"unitPrice":150,"lineTotal":300}],"savings":[],"subtotal":300,"total":300,"vat":32.14,"tendered":null,"change":null,"receiptNumber":null}""";

    private static async Task<HttpResponseMessage> PollAsync(HttpClient screen, long? known = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/customer-display/state");
        if (known is { } v)
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", $"\"v{v}\"");
        }

        return await screen.SendAsync(request);
    }

    [Fact]
    public async Task The_display_shows_what_its_register_pushed_and_asks_only_for_what_is_new()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var shop = await NewShopAsync(factory);
        var till = await NewTillAsync(shop);
        var screen = await NewScreenAsync(shop, till);

        // Nothing pushed yet: the display gets "no state" and shows its welcome screen.
        var first = (await (await PollAsync(screen.Client)).Content.ReadFromJsonAsync<Feed>(JsonOptions))!;
        Assert.Equal(0, first.Version);
        Assert.Null(first.State);

        Assert.Equal(HttpStatusCode.NoContent, (await till.Client.PutAsync("/customer-display/state", Json(Cart))).StatusCode);
        var shown = await PollAsync(screen.Client);
        Assert.Equal(HttpStatusCode.OK, shown.StatusCode);
        var feed = (await shown.Content.ReadFromJsonAsync<Feed>(JsonOptions))!;
        Assert.Equal(1, feed.Version);
        Assert.Equal("cart", feed.State!.Value.GetProperty("mode").GetString());
        Assert.Equal(300, feed.State.Value.GetProperty("total").GetDecimal());

        // Asking again with the version it has costs a 304, and a change bumps the version.
        Assert.Equal(HttpStatusCode.NotModified, (await PollAsync(screen.Client, feed.Version)).StatusCode);
        _ = await till.Client.PutAsync("/customer-display/state", Json("""{"mode":"idle","lines":[],"savings":[],"subtotal":0,"total":0,"vat":0}"""));
        var next = (await (await PollAsync(screen.Client, feed.Version)).Content.ReadFromJsonAsync<Feed>(JsonOptions))!;
        Assert.Equal(2, next.Version);
        Assert.Equal("idle", next.State!.Value.GetProperty("mode").GetString());
    }

    [Fact]
    public async Task A_display_never_sees_another_registers_cart()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var shop = await NewShopAsync(factory);
        var tillA = await NewTillAsync(shop, "Till A");
        var tillB = await NewTillAsync(shop, "Till B");
        var screenA = await NewScreenAsync(shop, tillA);
        _ = await NewScreenAsync(shop, tillB);

        _ = await tillB.Client.PutAsync("/customer-display/state", Json(Cart));

        var feed = (await (await PollAsync(screenA.Client)).Content.ReadFromJsonAsync<Feed>(JsonOptions))!;
        Assert.Equal(0, feed.Version);
        Assert.Null(feed.State);
    }

    [Fact]
    public async Task Nothing_is_stored_for_a_register_with_no_display_and_the_push_still_succeeds()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var shop = await NewShopAsync(factory);
        var till = await NewTillAsync(shop);

        Assert.Equal(HttpStatusCode.NoContent, (await till.Client.PutAsync("/customer-display/state", Json(Cart))).StatusCode);

        // A display paired afterwards starts blank rather than showing a stale cart.
        var screen = await NewScreenAsync(shop, till);
        Assert.Null((await (await PollAsync(screen.Client)).Content.ReadFromJsonAsync<Feed>(JsonOptions))!.State);
    }

    [Fact]
    public async Task Only_an_unlocked_register_can_push_and_only_a_display_can_read()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var shop = await NewShopAsync(factory);
        var till = await NewTillAsync(shop);
        var screen = await NewScreenAsync(shop, till);

        // An Admin on a personal device has no Register to speak for.
        Assert.Equal(HttpStatusCode.Forbidden, (await shop.Admin.PutAsync("/customer-display/state", Json(Cart))).StatusCode);
        // The display cannot push, and the till cannot read the display's feed.
        Assert.Equal(HttpStatusCode.Forbidden, (await screen.Client.PutAsync("/customer-display/state", Json(Cart))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await till.Client.GetAsync("/customer-display/state")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await shop.Anonymous.GetAsync("/customer-display/state")).StatusCode);
    }

    [Fact]
    public async Task A_malformed_or_oversized_state_is_refused()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var shop = await NewShopAsync(factory);
        var till = await NewTillAsync(shop);
        _ = await NewScreenAsync(shop, till);

        Assert.Equal(HttpStatusCode.BadRequest, (await till.Client.PutAsync("/customer-display/state", Json("""{"mode":"party"}"""))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await till.Client.PutAsync("/customer-display/state", Json("""{"lines":[]}"""))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await till.Client.PutAsync("/customer-display/state", Json("""["mode"]"""))).StatusCode);
        var huge = $$"""{"mode":"cart","note":"{{new string('x', CustomerDisplayService.MaxStateBytes)}}"}""";
        Assert.Equal(HttpStatusCode.BadRequest, (await till.Client.PutAsync("/customer-display/state", Json(huge))).StatusCode);
    }

    [Fact]
    public async Task A_revoked_display_stops_receiving()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var shop = await NewShopAsync(factory);
        var till = await NewTillAsync(shop);
        var screen = await NewScreenAsync(shop, till);
        _ = await till.Client.PutAsync("/customer-display/state", Json(Cart));
        Assert.Equal(HttpStatusCode.OK, (await PollAsync(screen.Client)).StatusCode);

        _ = await shop.Admin.PostAsync($"/devices/{screen.DeviceId}/revoke", null);

        Assert.Equal(HttpStatusCode.Unauthorized, (await PollAsync(screen.Client)).StatusCode);
    }
}
