using Microsoft.EntityFrameworkCore;
using Purch.Application.Auth;
using Purch.Domain.Entities;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfPasswordResetTokenRepository(PurchDbContext dbContext) : IPasswordResetTokenRepository
{
    public Task<PasswordResetToken?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        return dbContext.PasswordResetTokens
            .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);
    }

    public async Task<IReadOnlyList<PasswordResetToken>> ListOutstandingByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await dbContext.PasswordResetTokens
            .Where(token => token.UserId == userId && token.UsedAt == null && token.ExpiresAt > DateTimeOffset.UtcNow)
            .ToListAsync(cancellationToken);
    }

    public void Add(PasswordResetToken token)
    {
        _ = dbContext.PasswordResetTokens.Add(token);
    }
}
