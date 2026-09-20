using Microsoft.EntityFrameworkCore;
using Purch.Application.Auth;
using Purch.Domain.Entities;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfPasswordResetTokenRepository(PurchDbContext dbContext) : IPasswordResetTokenRepository
{
    public Task<PasswordResetToken?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        // Password reset is anonymous; the unguessable token hash is the credential.
        return dbContext.PasswordResetTokens
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);
    }

    public async Task<IReadOnlyList<PasswordResetToken>> ListOutstandingByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await dbContext.PasswordResetTokens
            .IgnoreQueryFilters()
            .Where(token => token.UserId == userId && token.UsedAt == null && token.ExpiresAt > DateTimeOffset.UtcNow)
            .ToListAsync(cancellationToken);
    }

    public void Add(PasswordResetToken token)
    {
        _ = dbContext.PasswordResetTokens.Add(token);
    }
}
