using Microsoft.EntityFrameworkCore;
using Purch.Application.Auth;
using Purch.Domain.Entities;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfUserRepository(PurchDbContext dbContext) : IUserRepository
{
    public async Task<User?> FindActorAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is not null)
        {
            return user;
        }

        var membership = await dbContext.Memberships.AsNoTracking().Include(m => m.Account).Include(m => m.Branches).FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        return membership is null ? null : MembershipUserProjection.ToUser(membership);
    }

    public async Task<IReadOnlyList<User>> ListActorsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var users = await dbContext.Users.AsNoTracking().Where(u => u.TenantId == tenantId).ToListAsync(cancellationToken);
        return [.. users, .. await ProjectedMembershipsAsync(tenantId, activeOnly: false, cancellationToken)];
    }

    public async Task<IReadOnlyList<User>> GetActiveActorsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var users = await dbContext.Users.AsNoTracking().Where(u => u.TenantId == tenantId && u.IsActive).ToListAsync(cancellationToken);
        return [.. users, .. await ProjectedMembershipsAsync(tenantId, activeOnly: true, cancellationToken)];
    }

    private async Task<List<User>> ProjectedMembershipsAsync(Guid tenantId, bool activeOnly, CancellationToken cancellationToken)
    {
        var memberships = await dbContext.Memberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(m => m.Account)
            .Include(m => m.Branches)
            .Where(m => m.TenantId == tenantId && (!activeOnly || m.IsActive))
            .ToListAsync(cancellationToken);
        return [.. memberships.Select(MembershipUserProjection.ToUser)];
    }

    public async Task<IReadOnlyList<User>> GetActiveUsersByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        // The tenant is an explicit argument (login resolves it from the device's pairing code), so
        // this is scoped by the query itself rather than by the ambient tenant.
        return await dbContext.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(user => user.TenantId == tenantId && user.IsActive)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<User>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Users
            .AsNoTracking()
            .Where(user => user.TenantId == tenantId)
            .ToListAsync(cancellationToken);
    }

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.Users.FirstOrDefaultAsync(user => user.Id == id, cancellationToken);
    }

    public Task<User?> GetByIdUnscopedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.Users.IgnoreQueryFilters().FirstOrDefaultAsync(user => user.Id == id, cancellationToken);
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        // ILIKE is only used for case-insensitivity: escape its wildcards so an email of "%" or "_"
        // can't match an arbitrary account.
        var normalized = email.Trim().Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
        // Admin login and password reset identify the tenant by the email itself, so this is cross-tenant.
        return dbContext.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                user => user.Email != null && EF.Functions.ILike(user.Email, normalized, "\\"),
                cancellationToken);
    }

    public void Add(User user)
    {
        _ = dbContext.Users.Add(user);
    }

    public async Task<IReadOnlyList<User>> ListUninvitedLegacyAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var linked = dbContext.Memberships.IgnoreQueryFilters().Where(m => m.TenantId == tenantId && m.LegacyUserId != null).Select(m => m.LegacyUserId!.Value);
        return await dbContext.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.IsActive && !linked.Contains(u.Id))
            .OrderBy(u => u.Name)
            .ToListAsync(cancellationToken);
    }
}
