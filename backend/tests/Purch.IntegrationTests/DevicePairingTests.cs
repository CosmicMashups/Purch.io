using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Purch.Application.Auth;
using Purch.Application.Devices;
using Purch.Application.Onboarding;
using Purch.Common.TestUtilities;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;
using Purch.Infrastructure.Persistence;

namespace Purch.IntegrationTests;

/// <summary>A device is created by an Admin, paired once with a code that works for ten minutes and one use, and then holds
/// its own revocable credential. Revoking or pairing again ends everything it was holding.</summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class DevicePairingTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string Password = "correct horse battery";

    private sealed record Tokens(string AccessToken, string RefreshToken);

    private sealed record Setup(HttpClient Client, BootstrapTenantResult Business);

    private static async Task<Setup> NewBusinessAsync(PurchApiFactory factory)
    {
        var client = factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@example.com";
        var bootstrap = await client.PostAsJsonAsync(
            "/onboarding/bootstrap",
            new BootstrapTenantRequest($"Store {Guid.NewGuid():N}", BusinessType.ConvenienceStore, "Main", "Ana", "123412", email, Password));
        var business = (await bootstrap.Content.ReadFromJsonAsync<BootstrapTenantResult>(JsonOptions))!;
        var tokens = (await (await client.PostAsJsonAsync("/auth/sign-in", new SignInRequest(email, Password))).Content.ReadFromJsonAsync<Tokens>(JsonOptions))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return new Setup(client, business);
    }

    private static async Task<DevicePairingCodeDto> CreateAsync(Setup setup, DeviceType type, string name = "Device", Guid? linked = null)
    {
        var response = await setup.Client.PostAsJsonAsync("/devices/pairing-requests", new CreateDevicePairingRequest(name, type, setup.Business.BranchId, linked));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<DevicePairingCodeDto>(JsonOptions))!;
    }

    private static async Task<PairedDeviceDto> PairAsync(HttpClient anonymous, string code)
    {
        var response = await anonymous.PostAsJsonAsync("/devices/pair", new PairDeviceRequest(code));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PairedDeviceDto>(JsonOptions))!;
    }

    private static Task<HttpResponseMessage> SessionAsync(HttpClient anonymous, string credential) =>
        anonymous.PostAsJsonAsync("/devices/session", new DeviceSessionRequest(credential));

    private PurchDbContext NewContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<PurchDbContext>().UseNpgsql(postgres.ConnectionString).Options;
        return new PurchDbContext(options, new TestCurrentTenantProvider { TenantId = tenantId });
    }

    [Fact]
    public async Task A_new_device_waits_for_its_code_which_works_once_and_hands_over_a_credential()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var setup = await NewBusinessAsync(factory);
        using var device = factory.CreateClient();

        var request = await CreateAsync(setup, DeviceType.Kiosk, "Entrance kiosk");

        Assert.Equal(DeviceStatus.Pending, request.Device.Status);
        Assert.Equal("Entrance kiosk", request.Device.Name);
        Assert.Equal(8, request.PairingCode.Length);
        Assert.InRange((request.ExpiresAt - DateTimeOffset.UtcNow).TotalMinutes, 9, 10.1);

        var paired = await PairAsync(device, request.PairingCode.ToLowerInvariant());
        Assert.Equal(request.Device.Id, paired.DeviceId);
        Assert.Equal(DeviceType.Kiosk, paired.DeviceType);
        Assert.False(string.IsNullOrWhiteSpace(paired.DeviceCredential));

        var again = await device.PostAsJsonAsync("/devices/pair", new PairDeviceRequest(request.PairingCode));
        Assert.Equal(HttpStatusCode.Unauthorized, again.StatusCode);

        var listed = await setup.Client.GetFromJsonAsync<List<DeviceDto>>("/devices", JsonOptions);
        var shown = listed!.Single(d => d.Id == request.Device.Id);
        Assert.Equal(DeviceStatus.Active, shown.Status);
        Assert.NotNull(shown.PairedAt);
        Assert.Null(shown.PairingCodeExpiresAt);
    }

    [Fact]
    public async Task An_expired_or_unknown_code_is_refused()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var setup = await NewBusinessAsync(factory);
        using var device = factory.CreateClient();
        var request = await CreateAsync(setup, DeviceType.OrderBoard);

        await using (var db = NewContext(setup.Business.TenantId))
        {
            var row = await db.Devices.SingleAsync(d => d.Id == request.Device.Id);
            row.PairingCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            _ = await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await device.PostAsJsonAsync("/devices/pair", new PairDeviceRequest(request.PairingCode))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await device.PostAsJsonAsync("/devices/pair", new PairDeviceRequest("ZZZZZZZZ"))).StatusCode);

        // A fresh code revives the waiting device.
        var fresh = (await (await setup.Client.PostAsync($"/devices/{request.Device.Id}/pairing-code", null)).Content.ReadFromJsonAsync<DevicePairingCodeDto>(JsonOptions))!;
        _ = await PairAsync(device, fresh.PairingCode);
    }

    [Theory]
    [InlineData(DeviceType.Kiosk, nameof(Role.Kiosk))]
    [InlineData(DeviceType.OrderBoard, nameof(Role.OrderBoard))]
    [InlineData(DeviceType.KitchenDisplay, nameof(Role.KitchenDisplay))]
    public async Task An_unattended_device_signs_itself_in_with_its_credential_and_gets_the_role_of_its_type(DeviceType type, string role)
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var setup = await NewBusinessAsync(factory);
        using var device = factory.CreateClient();
        var paired = await PairAsync(device, (await CreateAsync(setup, type)).PairingCode);

        var response = await SessionAsync(device, paired.DeviceCredential);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = (await response.Content.ReadFromJsonAsync<DeviceSessionDto>(JsonOptions))!;
        Assert.False(session.RequiresStaff);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(session.AccessToken);
        Assert.Equal(role, jwt.Claims.Single(c => c.Type == JwtClaimTypes.Role).Value);
        Assert.Equal(paired.DeviceId.ToString(), jwt.Claims.Single(c => c.Type == JwtClaimTypes.DeviceId).Value);
        Assert.DoesNotContain(jwt.Claims, c => c.Type == JwtRegisteredClaimNames.Sub);

        // The session renews through the ordinary refresh endpoint, and keeps the device's own role.
        var renewed = await device.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest(session.RefreshToken!));
        Assert.Equal(HttpStatusCode.OK, renewed.StatusCode);
        var renewedJwt = new JwtSecurityTokenHandler().ReadJwtToken((await renewed.Content.ReadFromJsonAsync<Tokens>(JsonOptions))!.AccessToken);
        Assert.Equal(role, renewedJwt.Claims.Single(c => c.Type == JwtClaimTypes.Role).Value);
    }

    [Theory]
    [InlineData(DeviceType.Register)]
    [InlineData(DeviceType.WarehouseOfficer)]
    public async Task A_register_or_warehouse_device_is_known_but_has_no_access_until_a_person_signs_in(DeviceType type)
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var setup = await NewBusinessAsync(factory);
        using var device = factory.CreateClient();
        var paired = await PairAsync(device, (await CreateAsync(setup, type)).PairingCode);

        var session = (await (await SessionAsync(device, paired.DeviceCredential)).Content.ReadFromJsonAsync<DeviceSessionDto>(JsonOptions))!;

        Assert.True(session.RequiresStaff);
        Assert.Null(session.AccessToken);
        Assert.Null(session.RefreshToken);
        Assert.Equal(type, session.DeviceType);
    }

    [Fact]
    public async Task A_customer_display_is_linked_to_a_register_in_the_same_branch_and_gets_its_own_role()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var setup = await NewBusinessAsync(factory);
        using var device = factory.CreateClient();
        var register = await CreateAsync(setup, DeviceType.Register, "Till 1");
        var kiosk = await CreateAsync(setup, DeviceType.Kiosk);

        var missing = await setup.Client.PostAsJsonAsync("/devices/pairing-requests", new CreateDevicePairingRequest("Screen", DeviceType.CustomerDisplay, setup.Business.BranchId));
        var notARegister = await setup.Client.PostAsJsonAsync("/devices/pairing-requests", new CreateDevicePairingRequest("Screen", DeviceType.CustomerDisplay, setup.Business.BranchId, kiosk.Device.Id));
        var linkedKiosk = await setup.Client.PostAsJsonAsync("/devices/pairing-requests", new CreateDevicePairingRequest("Kiosk", DeviceType.Kiosk, setup.Business.BranchId, register.Device.Id));
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, notARegister.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, linkedKiosk.StatusCode);

        var screen = await CreateAsync(setup, DeviceType.CustomerDisplay, "Screen", register.Device.Id);
        Assert.Equal(register.Device.Id, screen.Device.LinkedRegisterDeviceId);

        var paired = await PairAsync(device, screen.PairingCode);
        var session = (await (await SessionAsync(device, paired.DeviceCredential)).Content.ReadFromJsonAsync<DeviceSessionDto>(JsonOptions))!;
        Assert.Equal(nameof(Role.CustomerDisplay), new JwtSecurityTokenHandler().ReadJwtToken(session.AccessToken).Claims.Single(c => c.Type == JwtClaimTypes.Role).Value);
    }

    [Fact]
    public async Task Revoking_a_device_ends_its_credential_and_the_access_token_it_already_holds()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var setup = await NewBusinessAsync(factory);
        using var device = factory.CreateClient();
        var paired = await PairAsync(device, (await CreateAsync(setup, DeviceType.Kiosk)).PairingCode);
        var session = (await (await SessionAsync(device, paired.DeviceCredential)).Content.ReadFromJsonAsync<DeviceSessionDto>(JsonOptions))!;
        device.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await device.GetAsync("/tenant/settings")).StatusCode);

        var revoked = await setup.Client.PostAsync($"/devices/{paired.DeviceId}/revoke", null);

        Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
        Assert.Equal(DeviceStatus.Revoked, (await revoked.Content.ReadFromJsonAsync<DeviceDto>(JsonOptions))!.Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await device.GetAsync("/tenant/settings")).StatusCode);
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest(session.RefreshToken!))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SessionAsync(anonymous, paired.DeviceCredential)).StatusCode);
    }

    [Fact]
    public async Task Pairing_an_active_device_again_ends_the_old_credential_and_needs_the_new_code()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var setup = await NewBusinessAsync(factory);
        using var device = factory.CreateClient();
        var first = await PairAsync(device, (await CreateAsync(setup, DeviceType.KitchenDisplay)).PairingCode);

        var fresh = (await (await setup.Client.PostAsync($"/devices/{first.DeviceId}/pairing-code", null)).Content.ReadFromJsonAsync<DevicePairingCodeDto>(JsonOptions))!;

        Assert.Equal(DeviceStatus.Pending, fresh.Device.Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SessionAsync(device, first.DeviceCredential)).StatusCode);

        var second = await PairAsync(device, fresh.PairingCode);
        Assert.Equal(first.DeviceId, second.DeviceId);
        Assert.NotEqual(first.DeviceCredential, second.DeviceCredential);
        Assert.Equal(HttpStatusCode.OK, (await SessionAsync(device, second.DeviceCredential)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SessionAsync(device, first.DeviceCredential)).StatusCode);
    }

    [Fact]
    public async Task Only_an_admin_can_create_revoke_or_reissue_codes()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var setup = await NewBusinessAsync(factory);
        using var anonymous = factory.CreateClient();

        var create = await anonymous.PostAsJsonAsync("/devices/pairing-requests", new CreateDevicePairingRequest("X", DeviceType.Kiosk, setup.Business.BranchId));
        var revoke = await anonymous.PostAsync($"/devices/{Guid.NewGuid()}/revoke", null);

        Assert.Equal(HttpStatusCode.Unauthorized, create.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, revoke.StatusCode);
    }

    [Fact]
    public async Task A_device_in_another_business_cannot_be_revoked()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var mine = await NewBusinessAsync(factory);
        var theirs = await NewBusinessAsync(factory);
        var theirDevice = await CreateAsync(theirs, DeviceType.Kiosk);

        var response = await mine.Client.PostAsync($"/devices/{theirDevice.Device.Id}/revoke", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
