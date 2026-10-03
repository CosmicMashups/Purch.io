using Microsoft.EntityFrameworkCore;
using Purch.Application.Onboarding;
using Purch.Domain.Entities;
using Purch.Domain.Enums;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfMembershipRepository(PurchDbContext dbContext) : IMembershipRepository
{
    public async Task<IReadOnlyList<Membership>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Memberships
            .AsNoTracking()
            .Include(m => m.Account)
            .Include(m => m.Branches)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<Membership?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.Memberships
            .Include(m => m.Account)
            .Include(m => m.Branches)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public Task<bool> ExistsForEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return dbContext.Memberships.AnyAsync(m => m.Account!.Email == email, cancellationToken);
    }

    public Task<int> CountActiveAdminsAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.Memberships.CountAsync(m => m.Role == MembershipRole.Admin && m.IsActive, cancellationToken);
    }

    public Task<bool> ExistsAsync(Guid tenantId, Guid accountId, CancellationToken cancellationToken = default)
    {
        return dbContext.Memberships.IgnoreQueryFilters().AnyAsync(m => m.TenantId == tenantId && m.AccountId == accountId, cancellationToken);
    }

    public async Task<IReadOnlyList<Membership>> ListByTenantUnscopedAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Memberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(m => m.Account)
            .Include(m => m.Branches)
            .Where(m => m.TenantId == tenantId && m.IsActive)
            .ToListAsync(cancellationToken);
    }

    public Task<Membership?> GetUnscopedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.Memberships.IgnoreQueryFilters().FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }
}

public sealed class EfEnrolmentInviteRepository(PurchDbContext dbContext) : IEnrolmentInviteRepository
{
    public void Add(EnrolmentInvite invite)
    {
        _ = dbContext.EnrolmentInvites.Add(invite);
    }

    public async Task<IReadOnlyList<EnrolmentInvite>> ListPendingAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        return await dbContext.EnrolmentInvites
            .AsNoTracking()
            .Where(i => i.RedeemedAt == null && i.RevokedAt == null && i.ExpiresAt > now)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<EnrolmentInvite?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.EnrolmentInvites.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
    }

    public Task<EnrolmentInvite?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        return dbContext.EnrolmentInvites.IgnoreQueryFilters().FirstOrDefaultAsync(i => i.TokenHash == tokenHash, cancellationToken);
    }

    public Task<string?> GetBusinessNameAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return dbContext.Tenants.IgnoreQueryFilters().Where(t => t.Id == tenantId).Select(t => (string?)t.Name).FirstOrDefaultAsync(cancellationToken);
    }
}
