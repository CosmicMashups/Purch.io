using Microsoft.EntityFrameworkCore;
using Purch.Application.Auth;
using Purch.Domain.Entities;
using Purch.Domain.Enums;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Auth;

/// <summary>What the one-time move off the old sign-in did.</summary>
public sealed record LegacyMigrationReport(int OwnersMoved, int OwnersAlreadyMoved, int SessionsEnded, int DevicesToPairAgain, int StaffToInvite);

/// <summary>
/// Run once when the PIN-at-a-pairing-code sign-in is retired (<c>migrate-legacy</c> on the command line). It is safe to run
/// again.
/// <list type="bullet">
/// <item>Each owner who had an email and password gets an account and an Admin membership carrying the same password and PIN,
/// so they are not locked out. The password is carried as the hash it already is; nobody is asked to set it again.</item>
/// <item>Every session opened with the old sign-in ends, so nothing keeps renewing through a mechanism that is gone.</item>
/// <item>Every device paired the old way goes back to waiting for a one-time code, shown on the Devices page.</item>
/// <item>Staff who only had a PIN have no email to carry over. They are counted here and re-invited from the Staff page, which
/// lists them.</item>
/// </list>
/// </summary>
public sealed class LegacyMigration(PurchDbContext dbContext, IIdentityProvider identityProvider)
{
    public async Task<LegacyMigrationReport> RunAsync(CancellationToken cancellationToken = default)
    {
        var owners = await dbContext.Users
            .IgnoreQueryFilters()
            .Where(u => u.Role == Role.Admin && u.IsActive && u.Email != null && u.PasswordHash != null)
            .ToListAsync(cancellationToken);

        int moved = 0, already = 0;
        foreach (var owner in owners)
        {
            if (await dbContext.Memberships.IgnoreQueryFilters().AnyAsync(m => m.LegacyUserId == owner.Id, cancellationToken))
            {
                already++;
                continue;
            }

            var email = owner.Email!.Trim().ToLowerInvariant();
            var account = await dbContext.Accounts.FirstOrDefaultAsync(a => a.Email == email, cancellationToken);
            if (account is null)
            {
                var providerUserId = await identityProvider.CreateUserWithPasswordHashAsync(email, owner.PasswordHash!, cancellationToken);
                account = new Account { SupabaseUserId = providerUserId, Email = email, DisplayName = owner.Name };
                _ = dbContext.Accounts.Add(account);
            }

            // Already a member of this business (for example through an invitation): nothing to add.
            if (await dbContext.Memberships.IgnoreQueryFilters().AnyAsync(m => m.TenantId == owner.TenantId && m.AccountId == account.Id, cancellationToken))
            {
                already++;
                continue;
            }

            _ = dbContext.Memberships.Add(new Membership
            {
                TenantId = owner.TenantId,
                AccountId = account.Id,
                Role = MembershipRole.Admin,
                PinHash = owner.PinHash,
                LegacyUserId = owner.Id,
            });
            _ = await dbContext.SaveChangesAsync(cancellationToken);
            moved++;
        }

        var now = DateTimeOffset.UtcNow;

        // Devices paired the old way: no credential, and a permanent code. They wait for a one-time code now.
        var withCredentials = dbContext.DeviceCredentials.IgnoreQueryFilters().Select(c => c.DeviceId);
        var legacyDevices = await dbContext.Devices
            .IgnoreQueryFilters()
            .Where(d => d.PairingCode != string.Empty && d.Status == DeviceStatus.Active && !withCredentials.Contains(d.Id))
            .ToListAsync(cancellationToken);
        foreach (var device in legacyDevices)
        {
            device.Status = DeviceStatus.Pending;
            device.PairedAt = null;
            device.SessionVersion++;
        }

        // Sessions of the old sign-in: a staff or admin login (a user id), or a device paired with a PIN.
        var legacyDeviceIds = legacyDevices.Select(d => d.Id).ToList();
        var oldTokens = await dbContext.RefreshTokens
            .IgnoreQueryFilters()
            .Where(t => t.ExpiresAt > now && t.MembershipId == null && (t.UserId != null || (t.DeviceId != null && legacyDeviceIds.Contains(t.DeviceId.Value))))
            .ToListAsync(cancellationToken);
        foreach (var token in oldTokens)
        {
            token.RevokedAt = now;
            token.ExpiresAt = now;
        }

        _ = await dbContext.SaveChangesAsync(cancellationToken);

        var staffToInvite = await dbContext.Users
            .IgnoreQueryFilters()
            .Where(u => u.IsActive && !dbContext.Memberships.IgnoreQueryFilters().Any(m => m.LegacyUserId == u.Id))
            .CountAsync(cancellationToken);

        var report = new LegacyMigrationReport(moved, already, oldTokens.Count, legacyDevices.Count, staffToInvite);
        return report;
    }
}
