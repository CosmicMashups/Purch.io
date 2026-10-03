using Microsoft.EntityFrameworkCore;
using Purch.Application.Auth;
using Purch.Domain.Entities;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfAccountRepository(PurchDbContext dbContext) : IAccountRepository
{
    public Task<Account?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return dbContext.Accounts.FirstOrDefaultAsync(a => a.Email == email, cancellationToken);
    }

    public Task<Account?> FindByProviderUserIdAsync(Guid providerUserId, CancellationToken cancellationToken = default)
    {
        return dbContext.Accounts.FirstOrDefaultAsync(a => a.SupabaseUserId == providerUserId, cancellationToken);
    }

    public async Task<IReadOnlyList<(Membership Membership, string TenantName)>> ListActiveMembershipsAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        // The person has no session yet, so there is no tenant in context: read across businesses on purpose.
        var rows = await dbContext.Memberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(m => m.Branches)
            .Where(m => m.AccountId == accountId && m.IsActive)
            .Join(dbContext.Tenants.IgnoreQueryFilters(), m => m.TenantId, t => t.Id, (m, t) => new { Membership = m, TenantName = t.Name })
            .ToListAsync(cancellationToken);

        return rows.Select(r => (r.Membership, r.TenantName)).ToList();
    }

    public Task<Membership?> GetMembershipAsync(Guid membershipId, CancellationToken cancellationToken = default)
    {
        return dbContext.Memberships.IgnoreQueryFilters().Include(m => m.Branches).FirstOrDefaultAsync(m => m.Id == membershipId, cancellationToken);
    }

    public void Add(Account account) => dbContext.Accounts.Add(account);

    public void Add(Membership membership) => dbContext.Memberships.Add(membership);
}
