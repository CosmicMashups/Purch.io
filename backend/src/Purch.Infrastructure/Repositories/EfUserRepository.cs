using Microsoft.EntityFrameworkCore;
using Purch.Application.Auth;
using Purch.Domain.Entities;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfUserRepository(PurchDbContext dbContext) : IUserRepository
{
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
}
