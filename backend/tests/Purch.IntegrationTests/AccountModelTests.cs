using Microsoft.EntityFrameworkCore;
using Purch.Common.TestUtilities;
using Purch.Domain.Entities;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;
using Purch.Infrastructure.Persistence;

namespace Purch.IntegrationTests;

/// <summary>The sign-in redesign's tables: an account is one person across businesses, a membership is that person's place
/// in one business, and invites and device credentials are single-use secrets stored only as hashes.</summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class AccountModelTests(PostgresContainerFixture postgres)
{
    private PurchDbContext NewContext(Guid? tenantId)
    {
        var options = new DbContextOptionsBuilder<PurchDbContext>().UseNpgsql(postgres.ConnectionString).Options;
        return new PurchDbContext(options, new TestCurrentTenantProvider { TenantId = tenantId });
    }

    private static Account NewAccount(string email) => new() { SupabaseUserId = Guid.NewGuid(), Email = email, DisplayName = "Test" };

    [Fact]
    public async Task One_account_can_belong_to_two_businesses_with_different_roles_and_duties()
    {
        var accountEmail = $"{Guid.NewGuid():N}@example.com";
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var branchA = Guid.NewGuid();

        Guid accountId;
        await using (var seed = NewContext(null))
        {
            var account = NewAccount(accountEmail);
            _ = seed.Accounts.Add(account);
            _ = seed.Memberships.Add(new Membership
            {
                TenantId = tenantA,
                AccountId = account.Id,
                Role = MembershipRole.Staff,
                Duties = StaffDuty.Cashier | StaffDuty.Kitchen,
                Branches = [new MembershipBranch { TenantId = tenantA, BranchId = branchA }],
            });
            _ = seed.Memberships.Add(new Membership { TenantId = tenantB, AccountId = account.Id, Role = MembershipRole.Manager });
            _ = await seed.SaveChangesAsync();
            accountId = account.Id;
        }

        await using var asA = NewContext(tenantA);
        var inA = await asA.Memberships.Include(m => m.Branches).SingleAsync(m => m.AccountId == accountId);
        Assert.Equal(StaffDuty.Cashier | StaffDuty.Kitchen, inA.Duties);
        Assert.Equal([branchA], inA.Branches.Select(b => b.BranchId).ToList());

        await using var asB = NewContext(tenantB);
        var inB = await asB.Memberships.SingleAsync(m => m.AccountId == accountId);
        Assert.Equal(MembershipRole.Manager, inB.Role);

        // Each business only ever sees its own membership row.
        Assert.Single(await asA.Memberships.Where(m => m.AccountId == accountId).ToListAsync());
    }

    [Fact]
    public async Task An_email_can_only_have_one_account_and_one_membership_per_business()
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        var tenantId = Guid.NewGuid();
        await using var dbContext = NewContext(tenantId);
        var account = NewAccount(email);
        _ = dbContext.Accounts.Add(account);
        _ = dbContext.Memberships.Add(new Membership { TenantId = tenantId, AccountId = account.Id });
        _ = await dbContext.SaveChangesAsync();

        await using var again = NewContext(tenantId);
        _ = again.Accounts.Add(NewAccount(email));
        _ = await Assert.ThrowsAsync<DbUpdateException>(() => again.SaveChangesAsync());

        await using var duplicateMembership = NewContext(tenantId);
        _ = duplicateMembership.Memberships.Add(new Membership { TenantId = tenantId, AccountId = account.Id });
        _ = await Assert.ThrowsAsync<DbUpdateException>(() => duplicateMembership.SaveChangesAsync());
    }

    [Fact]
    public async Task An_invite_keeps_its_branch_list_and_is_found_by_its_token_hash_without_a_tenant()
    {
        var tenantId = Guid.NewGuid();
        var branches = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var hash = Guid.NewGuid().ToString("N");

        await using (var seed = NewContext(tenantId))
        {
            _ = seed.EnrolmentInvites.Add(new EnrolmentInvite
            {
                TenantId = tenantId,
                Purpose = InvitePurpose.Enrolment,
                Name = "Ana",
                Email = "ana@example.com",
                Role = MembershipRole.Staff,
                Duties = StaffDuty.Warehouse,
                BranchIds = branches,
                TokenHash = hash,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(2),
            });
            _ = await seed.SaveChangesAsync();
        }

        // Someone opening the link has no session, so the lookup has to ignore the tenant filter.
        await using var anonymous = NewContext(null);
        Assert.Empty(await anonymous.EnrolmentInvites.Where(i => i.TokenHash == hash).ToListAsync());
        var found = await anonymous.EnrolmentInvites.IgnoreQueryFilters().SingleAsync(i => i.TokenHash == hash);
        Assert.Equal(branches, found.BranchIds);
        Assert.Equal(StaffDuty.Warehouse, found.Duties);
    }

    [Fact]
    public async Task A_device_created_before_pairing_existed_reads_as_active_and_new_devices_need_no_permanent_code()
    {
        var tenantId = Guid.NewGuid();
        await using var dbContext = NewContext(tenantId);

        // Two pending devices both have an empty permanent code; the unique index must not trip over that.
        _ = dbContext.Devices.Add(new Device { TenantId = tenantId, BranchId = Guid.NewGuid(), PairingCode = string.Empty, Status = DeviceStatus.Pending, PairingCodeHash = Guid.NewGuid().ToString("N"), PairingCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10), DeviceType = DeviceType.CustomerDisplay });
        _ = dbContext.Devices.Add(new Device { TenantId = tenantId, BranchId = Guid.NewGuid(), PairingCode = string.Empty, Status = DeviceStatus.Pending, PairingCodeHash = Guid.NewGuid().ToString("N"), PairingCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10) });
        _ = await dbContext.SaveChangesAsync();

        Assert.Equal(DeviceStatus.Active, new Device().Status);
        Assert.Equal(DeviceStatus.Active, default(DeviceStatus));
    }

    [Fact]
    public async Task A_device_credential_hash_is_unique()
    {
        var tenantId = Guid.NewGuid();
        var hash = Guid.NewGuid().ToString("N");
        await using var dbContext = NewContext(tenantId);
        _ = dbContext.DeviceCredentials.Add(new DeviceCredential { TenantId = tenantId, DeviceId = Guid.NewGuid(), CredentialHash = hash });
        _ = await dbContext.SaveChangesAsync();

        await using var again = NewContext(tenantId);
        _ = again.DeviceCredentials.Add(new DeviceCredential { TenantId = tenantId, DeviceId = Guid.NewGuid(), CredentialHash = hash });
        _ = await Assert.ThrowsAsync<DbUpdateException>(() => again.SaveChangesAsync());
    }
}
