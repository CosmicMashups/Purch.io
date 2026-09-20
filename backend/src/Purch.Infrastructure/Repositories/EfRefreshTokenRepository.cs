using Microsoft.EntityFrameworkCore;
using Purch.Application.Auth;
using Purch.Domain.Entities;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfRefreshTokenRepository(PurchDbContext dbContext) : IRefreshTokenRepository
{
    public Task<RefreshToken?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        return dbContext.RefreshTokens
            .FirstOrDefaultAsync(refreshToken => refreshToken.TokenHash == tokenHash, cancellationToken);
    }

    public async Task<IReadOnlyList<RefreshToken>> ListActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await dbContext.RefreshTokens
            .Where(token => token.UserId == userId && token.ExpiresAt > DateTimeOffset.UtcNow)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RefreshToken>> ListActiveByDeviceIdAsync(Guid deviceId, CancellationToken cancellationToken = default)
    {
        return await dbContext.RefreshTokens
            .Where(token => token.DeviceId == deviceId && token.ExpiresAt > DateTimeOffset.UtcNow)
            .ToListAsync(cancellationToken);
    }

    public void Add(RefreshToken refreshToken)
    {
        _ = dbContext.RefreshTokens.Add(refreshToken);
    }
}
