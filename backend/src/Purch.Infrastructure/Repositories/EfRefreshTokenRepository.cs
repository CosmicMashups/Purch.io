using Microsoft.EntityFrameworkCore;
using Purch.Application.Auth;
using Purch.Domain.Entities;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfRefreshTokenRepository(PurchDbContext dbContext) : IRefreshTokenRepository
{
    public Task<RefreshToken?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        // Refresh runs before any tenant is known; the unguessable token hash is the credential.
        return dbContext.RefreshTokens
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(refreshToken => refreshToken.TokenHash == tokenHash, cancellationToken);
    }

    public async Task<IReadOnlyList<RefreshToken>> ListActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await dbContext.RefreshTokens
            .IgnoreQueryFilters()
            .Where(token => token.UserId == userId && token.ExpiresAt > DateTimeOffset.UtcNow)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RefreshToken>> ListActiveByDeviceIdAsync(Guid deviceId, CancellationToken cancellationToken = default)
    {
        return await dbContext.RefreshTokens
            .IgnoreQueryFilters()
            .Where(token => token.DeviceId == deviceId && token.ExpiresAt > DateTimeOffset.UtcNow)
            .ToListAsync(cancellationToken);
    }

    public void Add(RefreshToken refreshToken)
    {
        _ = dbContext.RefreshTokens.Add(refreshToken);
    }
}
