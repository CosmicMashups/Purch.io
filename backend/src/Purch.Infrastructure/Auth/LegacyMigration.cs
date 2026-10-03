using Microsoft.EntityFrameworkCore;
using Purch.Application.Auth;
using Purch.Application.Onboarding;
using Purch.Domain.Entities;
using Purch.Domain.Enums;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Auth;

/// <summary>What the one-time move off the old sign-in did.</summary>
public sealed record LegacyMigrationReport(int OwnersMoved, int OwnersAlreadyMoved, int SessionsEnded, int DevicesToPairAgain, int StaffToInvite, IReadOnlyList<OwnerClaimLink> OwnerLinks);

/// <summary>A single-use link for an owner who never set an email and password. Shown once, to be handed to them.</summary>
public sealed record OwnerClaimLink(string Business, string OwnerName, string Token);

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

        var links = await IssueOwnerClaimLinksAsync(cancellationToken);

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

        var report = new LegacyMigrationReport(moved, already, oldTokens.Count, legacyDevices.Count, staffToInvite, links);
        return report;
    }

    /// <summary>Owners who only had a PIN have no email or password to carry over, so nobody could sign in to invite them. Each gets
    /// a single-use link that lets them choose an email, password and PIN and become the Admin again, with their history intact.
    /// Running it again replaces a link that was lost.</summary>
    private async Task<IReadOnlyList<OwnerClaimLink>> IssueOwnerClaimLinksAsync(CancellationToken cancellationToken)
    {
        var owners = await dbContext.Users
            .IgnoreQueryFilters()
            .Where(u => u.Role == Role.Admin && u.IsActive && (u.Email == null || u.PasswordHash == null)
                && !dbContext.Memberships.IgnoreQueryFilters().Any(m => m.LegacyUserId == u.Id))
            .ToListAsync(cancellationToken);

        var links = new List<OwnerClaimLink>();
        foreach (var owner in owners)
        {
            var stale = await dbContext.EnrolmentInvites.IgnoreQueryFilters()
                .Where(i => i.LegacyUserId == owner.Id && i.RedeemedAt == null && i.RevokedAt == null)
                .ToListAsync(cancellationToken);
            foreach (var old in stale)
            {
                old.RevokedAt = DateTimeOffset.UtcNow;
            }

            var invite = new EnrolmentInvite
            {
                TenantId = owner.TenantId,
                Purpose = InvitePurpose.Enrolment,
                LegacyUserId = owner.Id,
                Name = owner.Name,
                Email = owner.Email?.Trim().ToLowerInvariant() ?? string.Empty,
                Role = MembershipRole.Admin,
            };
            var token = StaffEnrolmentService.StageToken(invite, TimeSpan.FromDays(30));
            _ = dbContext.EnrolmentInvites.Add(invite);
            var business = await dbContext.Tenants.IgnoreQueryFilters().Where(t => t.Id == owner.TenantId).Select(t => t.Name).FirstOrDefaultAsync(cancellationToken) ?? string.Empty;
            links.Add(new OwnerClaimLink(business, owner.Name, token));
        }

        _ = await dbContext.SaveChangesAsync(cancellationToken);
        return links;
    }
}
