using System.Net;
using System.Net.Http.Headers;
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

/// <summary>The one-time move off the old PIN-at-a-pairing-code sign-in: owners are carried over with their password and PIN, old
/// sessions end, old devices wait for a one-time code, and staff who only had a PIN are re-invited from the Staff page.</summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class LegacyMigrationTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string OwnerPassword = "the owner's old password";

    private sealed record Tokens(string AccessToken, string RefreshToken);

    private sealed record Seeded(Guid TenantId, Guid BranchId, Guid OwnerId, Guid CashierId, Guid LegacyDeviceId, Guid NewDeviceId, string OwnerEmail, string NewDeviceToken);

    private PurchDbContext NewContext(Guid? tenantId)
    {
        var options = new DbContextOptionsBuilder<PurchDbContext>().UseNpgsql(postgres.ConnectionString).Options;
        return new PurchDbContext(options, new TestCurrentTenantProvider { TenantId = tenantId });
    }

    /// <summary>A business as the old sign-in left it: an owner with email and password, a cashier with only a PIN, a device paired with
    /// a permanent code, and a device that was already paired the new way.</summary>
    private async Task<Seeded> SeedAsync()
    {
        var tenantId = Guid.NewGuid();
        var email = $"{Guid.NewGuid():N}@example.com";
        await using var db = NewContext(tenantId);
        var newDeviceToken = $"new-device-token-{Guid.NewGuid():N}";
        _ = db.Tenants.Add(new Tenant { Id = tenantId, Name = $"Old Shop {tenantId:N}", BusinessType = BusinessType.ConvenienceStore });
        var branch = new Branch { TenantId = tenantId, Name = "Main" };
        var owner = new User { TenantId = tenantId, Name = "Old Owner", Email = email, Role = Role.Admin, ScopeType = ScopeType.Tenant, PinHash = new BCryptPinHasher().Hash("1234"), PasswordHash = new BCryptPasswordHasher().Hash(OwnerPassword) };
        var cashier = new User { TenantId = tenantId, Name = "Old Cashier", Role = Role.Cashier, ScopeType = ScopeType.Tenant, BranchId = branch.Id, PinHash = new BCryptPinHasher().Hash("5678") };
        var legacyDevice = new Device { TenantId = tenantId, BranchId = branch.Id, PairingCode = $"OLD-{Guid.NewGuid():N}"[..12], DeviceType = DeviceType.Register };
        var newDevice = new Device { TenantId = tenantId, BranchId = branch.Id, PairingCode = string.Empty, DeviceType = DeviceType.Kiosk, Name = "New kiosk", PairedAt = DateTimeOffset.UtcNow };
        _ = db.Branches.Add(branch);
        db.Users.AddRange(owner, cashier);
        db.Devices.AddRange(legacyDevice, newDevice);
        _ = db.DeviceCredentials.Add(new DeviceCredential { TenantId = tenantId, DeviceId = newDevice.Id, CredentialHash = Guid.NewGuid().ToString("N") });
        var future = DateTimeOffset.UtcNow.AddDays(20);
        db.RefreshTokens.AddRange(
            new RefreshToken { TenantId = tenantId, UserId = owner.Id, TokenHash = Guid.NewGuid().ToString("N"), ExpiresAt = future },
            new RefreshToken { TenantId = tenantId, UserId = cashier.Id, DeviceId = legacyDevice.Id, TokenHash = Guid.NewGuid().ToString("N"), ExpiresAt = future },
            new RefreshToken { TenantId = tenantId, DeviceId = legacyDevice.Id, TokenHash = Guid.NewGuid().ToString("N"), ExpiresAt = future },
            new RefreshToken { TenantId = tenantId, DeviceId = newDevice.Id, TokenHash = newDeviceToken, ExpiresAt = future });
        _ = await db.SaveChangesAsync();
        return new Seeded(tenantId, branch.Id, owner.Id, cashier.Id, legacyDevice.Id, newDevice.Id, email, newDeviceToken);
    }

    private static async Task<LegacyMigrationReport> MigrateAsync(PurchApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<LegacyMigration>().RunAsync();
    }

    [Fact]
    public async Task The_owner_keeps_their_password_and_pin_and_everything_old_is_cut_off()
    {
        var seeded = await SeedAsync();
        await using var factory = new PurchApiFactory(postgres.ConnectionString);

        var report = await MigrateAsync(factory);

        Assert.True(report.OwnersMoved >= 1);
        using var client = factory.CreateClient();
        var signIn = await client.PostAsJsonAsync("/auth/sign-in", new SignInRequest(seeded.OwnerEmail, OwnerPassword));
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/auth/sign-in", new SignInRequest(seeded.OwnerEmail, "a different password"))).StatusCode);

        await using var db = NewContext(seeded.TenantId);
        var membership = await db.Memberships.Include(m => m.Account).SingleAsync();
        Assert.Equal(MembershipRole.Admin, membership.Role);
        Assert.Equal(seeded.OwnerId, membership.LegacyUserId);
        Assert.Equal(seeded.OwnerEmail, membership.Account!.Email);
        Assert.True(new BCryptPinHasher().Verify("1234", membership.PinHash!));

        // Sessions of the old sign-in end; a device paired the new way keeps its own.
        var tokens = await db.RefreshTokens.ToListAsync();
        Assert.All(tokens.Where(t => t.MembershipId == null && t.TokenHash != seeded.NewDeviceToken), t => Assert.True(t.ExpiresAt <= DateTimeOffset.UtcNow));
        Assert.True(tokens.Single(t => t.TokenHash == seeded.NewDeviceToken).ExpiresAt > DateTimeOffset.UtcNow);

        // The device paired the old way waits for a one-time code; the one paired the new way is untouched.
        var devices = await db.Devices.ToListAsync();
        Assert.Equal(DeviceStatus.Pending, devices.Single(d => d.Id == seeded.LegacyDeviceId).Status);
        Assert.Equal(DeviceStatus.Active, devices.Single(d => d.Id == seeded.NewDeviceId).Status);
    }

    [Fact]
    public async Task Running_it_again_changes_nothing_more()
    {
        var seeded = await SeedAsync();
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        _ = await MigrateAsync(factory);

        await using var after = NewContext(seeded.TenantId);
        var versionBefore = (await after.Devices.SingleAsync(d => d.Id == seeded.LegacyDeviceId)).SessionVersion;

        var second = await MigrateAsync(factory);

        Assert.Equal(0, second.OwnersMoved);
        Assert.Equal(0, second.DevicesToPairAgain);
        await using var check = NewContext(seeded.TenantId);
        Assert.Single(await check.Memberships.ToListAsync());
        Assert.Equal(versionBefore, (await check.Devices.SingleAsync(d => d.Id == seeded.LegacyDeviceId)).SessionVersion);
    }

    [Fact]
    public async Task Staff_who_only_had_a_pin_are_listed_and_re_invited_with_their_history_carried_over()
    {
        var seeded = await SeedAsync();
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        _ = await MigrateAsync(factory);
        using var anonymous = factory.CreateClient();
        var owner = (await (await anonymous.PostAsJsonAsync("/auth/sign-in", new SignInRequest(seeded.OwnerEmail, OwnerPassword))).Content.ReadFromJsonAsync<Tokens>(JsonOptions))!;
        using var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", owner.AccessToken);

        var legacy = (await admin.GetFromJsonAsync<List<LegacyStaffDto>>("/staff/legacy", JsonOptions))!;
        var listed = Assert.Single(legacy);
        Assert.Equal(seeded.CashierId, listed.Id);
        Assert.Equal("Old Cashier", listed.Name);
        Assert.Equal(MembershipRole.Staff, listed.SuggestedRole);
        Assert.Equal(StaffDuty.Cashier, listed.SuggestedDuties);

        // Someone who is not in the old list cannot be named.
        var stranger = await admin.PostAsJsonAsync("/staff/invites", new CreateInviteRequest("X", "x@example.com", MembershipRole.Staff, StaffDuty.Cashier, [seeded.BranchId], Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.NotFound, stranger.StatusCode);

        var email = $"{Guid.NewGuid():N}@example.com";
        var invite = (await (await admin.PostAsJsonAsync("/staff/invites", new CreateInviteRequest("Old Cashier", email, MembershipRole.Staff, StaffDuty.Cashier, [seeded.BranchId], seeded.CashierId))).Content.ReadFromJsonAsync<InviteLinkDto>(JsonOptions))!;
        var redeemed = await anonymous.PostAsJsonAsync("/enrol/redeem", new RedeemInviteRequest(invite.Token, "a fresh passphrase", "4821"));
        Assert.Equal(HttpStatusCode.OK, redeemed.StatusCode);

        // Their earlier sales and shifts stay theirs through the carried-over id, and they drop off the list.
        await using var db = NewContext(seeded.TenantId);
        Assert.Equal(seeded.CashierId, (await db.Memberships.SingleAsync(m => m.Account!.Email == email)).LegacyUserId);
        Assert.Empty((await admin.GetFromJsonAsync<List<LegacyStaffDto>>("/staff/legacy", JsonOptions))!);
    }

    [Fact]
    public async Task The_report_says_how_many_staff_still_need_inviting()
    {
        _ = await SeedAsync();
        await using var factory = new PurchApiFactory(postgres.ConnectionString);

        var report = await MigrateAsync(factory);

        Assert.True(report.StaffToInvite >= 1);
        Assert.True(report.DevicesToPairAgain >= 1);
    }
}
