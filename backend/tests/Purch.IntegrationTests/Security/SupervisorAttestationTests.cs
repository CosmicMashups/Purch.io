using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Purch.Application.Auth;
using Purch.Application.Catalog;
using Purch.Application.Onboarding;
using Purch.Application.Pos;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests.Security;

/// <summary>An offline Senior/PWD sale syncs later, often under a cashier's login, so the server cannot ask the manager for a PIN
/// then. It trusts a signed, till-bound, time-limited attestation the till received when that manager signed in, and never an id
/// the till merely states.</summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class SupervisorAttestationTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string ManagerPin = "567812";

    private sealed record Scene(PurchApiFactory Factory, HttpClient Admin, HttpClient Cashier, Guid ManagerId, ItemDto Item);

    private static async Task<Scene> SceneAsync(PurchApiFactory factory)
    {
        var admin = await TestSessions.AdminClientAsync(factory);
        var managerId = await TestSessions.AddPersonAsync(admin, "Mae Manager", MembershipRole.Manager, StaffDuty.None, ManagerPin);
        var cashier = await TestSessions.CashierClientAsync(admin);
        var item = (await (await admin.PostAsJsonAsync("/items", new CreateItemRequest("Medicine", null, null, null, 100m, null, PricingType.Unit))).Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;
        return new Scene(factory, admin, cashier, managerId, item);
    }

    private static CheckoutRequest OfflineSale(ItemDto item, Guid? rungBy, string? attestation, DateTimeOffset? soldAt = null) => new(
        Guid.NewGuid(),
        [new AddTransactionLineRequest(item.Id, null, 1m)],
        SeniorPwdDiscountApplied: true,
        PromoCode: null,
        OrderType: null,
        Payment: new RecordPaymentRequest(PaymentMethod.Cash, 500m),
        ExpectedTotal: null,
        ReceiptNumber: null,
        OfflineSale: true,
        SoldAt: soldAt ?? DateTimeOffset.UtcNow,
        RungByStaffId: rungBy,
        SupervisorAttestation: attestation);

    private static Task<HttpResponseMessage> CheckoutAsync(Scene scene, CheckoutRequest request) => scene.Cashier.PostAsJsonAsync("/transactions/checkout", request);

    [Fact]
    public async Task A_manager_gets_an_attestation_when_unlocking_and_a_cashier_does_not()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var scene = await SceneAsync(factory);
        using var _admin = scene.Admin;
        using var _cashier = scene.Cashier;

        Assert.False(string.IsNullOrEmpty(await TestSessions.SupervisorAttestationAsync(scene.Admin, scene.ManagerId, ManagerPin)));

        var cashierId = await TestSessions.AddPersonAsync(scene.Admin, "Cora Cashier", MembershipRole.Staff, StaffDuty.Cashier, "246802");
        Assert.Null(await TestSessions.SupervisorAttestationAsync(scene.Admin, cashierId, "246802"));
    }

    [Fact]
    public async Task A_discount_sale_with_only_a_stated_manager_id_is_refused()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var scene = await SceneAsync(factory);
        using var _admin = scene.Admin;
        using var _cashier = scene.Cashier;

        // Manager ids are visible to anyone holding a till's roster, so naming one proves nothing.
        var response = await CheckoutAsync(scene, OfflineSale(scene.Item, scene.ManagerId, attestation: null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_genuine_attestation_lets_a_cashier_sync_the_managers_discount_sale()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var scene = await SceneAsync(factory);
        using var _admin = scene.Admin;
        using var _cashier = scene.Cashier;
        var attestation = await TestSessions.SupervisorAttestationAsync(scene.Admin, scene.ManagerId, ManagerPin);

        var response = await CheckoutAsync(scene, OfflineSale(scene.Item, scene.ManagerId, attestation));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_tampered_or_garbled_attestation_is_refused()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var scene = await SceneAsync(factory);
        using var _admin = scene.Admin;
        using var _cashier = scene.Cashier;
        var good = (await TestSessions.SupervisorAttestationAsync(scene.Admin, scene.ManagerId, ManagerPin))!;
        var flipped = good[..^2] + (good[^2] == 'A' ? 'B' : 'A') + good[^1];

        Assert.Equal(HttpStatusCode.Forbidden, (await CheckoutAsync(scene, OfflineSale(scene.Item, scene.ManagerId, flipped))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await CheckoutAsync(scene, OfflineSale(scene.Item, scene.ManagerId, "v1.not.valid"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await CheckoutAsync(scene, OfflineSale(scene.Item, scene.ManagerId, "complete garbage"))).StatusCode);
    }

    [Fact]
    public async Task An_attestation_from_another_register_does_not_work_on_this_one()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var scene = await SceneAsync(factory);
        using var _admin = scene.Admin;
        using var _cashier = scene.Cashier;
        var shop = TestSessions.ShopOf(scene.Admin);
        using var anonymous = factory.CreateClient();
        var (_, otherCredential) = await TestSessions.PairAsync(scene.Admin, anonymous, shop.Tenant.BranchId, DeviceType.Register, "Back counter");
        var fromOtherRegister = await TestSessions.SupervisorAttestationAsync(scene.Admin, scene.ManagerId, ManagerPin, otherCredential);

        var response = await CheckoutAsync(scene, OfflineSale(scene.Item, scene.ManagerId, fromOtherRegister));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_attestation_covers_the_time_of_the_sale_not_the_time_it_syncs()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var scene = await SceneAsync(factory);
        using var _admin = scene.Admin;
        using var _cashier = scene.Cashier;
        var shop = TestSessions.ShopOf(scene.Admin);
        var attestations = factory.Services.GetRequiredService<ISupervisorAttestationService>();

        // The manager signed in 13 hours ago, so the attestation ran out an hour ago.
        var signedInAt = DateTimeOffset.UtcNow.AddHours(-13);
        var token = attestations.Issue(scene.ManagerId, shop.Tenant.TenantId, shop.RegisterId, signedInAt).Token;

        // A sale made now, long after it expired, is refused.
        Assert.Equal(HttpStatusCode.Forbidden, (await CheckoutAsync(scene, OfflineSale(scene.Item, scene.ManagerId, token))).StatusCode);

        // A sale made 12 hours ago, inside its window, still syncs fine now that the connection is back.
        var madeInsideWindow = DateTimeOffset.UtcNow.AddHours(-12);
        Assert.Equal(HttpStatusCode.OK, (await CheckoutAsync(scene, OfflineSale(scene.Item, scene.ManagerId, token, madeInsideWindow))).StatusCode);

        // And one claiming to predate the sign-in is refused.
        var beforeSignIn = DateTimeOffset.UtcNow.AddHours(-14);
        Assert.Equal(HttpStatusCode.Forbidden, (await CheckoutAsync(scene, OfflineSale(scene.Item, scene.ManagerId, token, beforeSignIn))).StatusCode);
    }

    [Fact]
    public async Task An_attestation_for_someone_who_is_no_longer_a_supervisor_is_refused()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var scene = await SceneAsync(factory);
        using var _admin = scene.Admin;
        using var _cashier = scene.Cashier;
        var shop = TestSessions.ShopOf(scene.Admin);
        var cashierId = await TestSessions.AddPersonAsync(scene.Admin, "Cora Cashier", MembershipRole.Staff, StaffDuty.Cashier, "246802");
        var token = factory.Services.GetRequiredService<ISupervisorAttestationService>()
            .Issue(cashierId, shop.Tenant.TenantId, shop.RegisterId, DateTimeOffset.UtcNow).Token;

        // Genuine signature, but the person named is only a cashier: a signature alone never grants the role.
        var response = await CheckoutAsync(scene, OfflineSale(scene.Item, cashierId, token));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task During_the_upgrade_grace_period_a_stated_manager_id_is_accepted_but_a_stated_cashier_id_never_is()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString) { ExtraSettings = { ["POS_ACCEPT_UNATTESTED_OFFLINE_DISCOUNTS"] = "true" } };
        var scene = await SceneAsync(factory);
        using var _admin = scene.Admin;
        using var _cashier = scene.Cashier;
        var cashierId = await TestSessions.AddPersonAsync(scene.Admin, "Cora Cashier", MembershipRole.Staff, StaffDuty.Cashier, "246802");

        Assert.Equal(HttpStatusCode.OK, (await CheckoutAsync(scene, OfflineSale(scene.Item, scene.ManagerId, attestation: null))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await CheckoutAsync(scene, OfflineSale(scene.Item, cashierId, attestation: null))).StatusCode);
    }

    [Fact]
    public void Attestations_are_bound_to_the_business_the_register_and_the_clock()
    {
        var service = new Purch.Infrastructure.Auth.SupervisorAttestationService(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["JWT_SIGNING_KEY"] = "a-test-signing-key-that-is-long-enough-32b" })
                .Build());
        var (member, tenant, device, now) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);

        var token = service.Issue(member, tenant, device, now).Token;

        Assert.Equal(member, service.Validate(token, tenant, device, now.AddHours(1)));
        Assert.Null(service.Validate(token, Guid.NewGuid(), device, now));
        Assert.Null(service.Validate(token, tenant, Guid.NewGuid(), now));
        Assert.Null(service.Validate(token, tenant, device, now.AddHours(13)));
        Assert.Null(service.Validate(token, tenant, device, now.AddMinutes(-5)));
        Assert.Null(service.Validate(string.Empty, tenant, device, now));
    }
}
