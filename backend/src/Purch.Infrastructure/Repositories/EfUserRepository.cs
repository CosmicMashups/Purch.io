using Microsoft.EntityFrameworkCore;
using Purch.Application.Auth;
using Purch.Domain.Entities;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfUserRepository(PurchDbContext dbContext) : IUserRepository
{
    public async Task<IReadOnlyList<User>> GetActiveUsersByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Users
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

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        // ILIKE is only used for case-insensitivity: escape its wildcards so an email of "%" or "_"
        // can't match an arbitrary account.
        var normalized = email.Trim().Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
        return dbContext.Users
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
