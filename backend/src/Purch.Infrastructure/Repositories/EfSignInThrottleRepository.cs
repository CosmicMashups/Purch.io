using Microsoft.EntityFrameworkCore;
using Purch.Application.Auth;
using Purch.Domain.Entities;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfSignInThrottleRepository(PurchDbContext dbContext) : ISignInThrottleRepository
{
    public async Task<SignInThrottleState?> GetAsync(string emailHash, CancellationToken cancellationToken = default)
    {
        var row = await dbContext.Set<SignInThrottle>()
            .AsNoTracking()
            .Where(t => t.EmailHash == emailHash)
            .Select(t => new { t.BlockedUntil })
            .FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : new SignInThrottleState(row.BlockedUntil);
    }

    public async Task RecordFailureAsync(string emailHash, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var windowStart = now - SignInThrottlePolicy.Window;
        var freeAttempts = SignInThrottlePolicy.FreeAttempts;
        var maxBlockMinutes = SignInThrottlePolicy.MaxBlock.TotalMinutes;

        // One atomic statement per step, so parallel guesses each count (a read-modify-write would let them
        // all read the same number). The first statement counts the miss and restarts a stale run; the second
        // sets the block from the stored count.
        _ = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "SignInThrottles" ("Id", "CreatedAt", "EmailHash", "Failures", "WindowStartedAt")
            VALUES ({Guid.NewGuid()}, {now}, {emailHash}, 1, {now})
            ON CONFLICT ("EmailHash") DO UPDATE SET
                "Failures" = CASE WHEN "SignInThrottles"."WindowStartedAt" < {windowStart} THEN 1 ELSE "SignInThrottles"."Failures" + 1 END,
                "WindowStartedAt" = CASE WHEN "SignInThrottles"."WindowStartedAt" < {windowStart} THEN {now} ELSE "SignInThrottles"."WindowStartedAt" END,
                "UpdatedAt" = {now}
            """, cancellationToken);

        _ = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "SignInThrottles"
            SET "BlockedUntil" = CASE WHEN "Failures" >= {freeAttempts}
                THEN {now} + LEAST(POWER(2, LEAST("Failures" - {freeAttempts}, 10)), {maxBlockMinutes}) * INTERVAL '1 minute'
                ELSE NULL END
            WHERE "EmailHash" = {emailHash}
            """, cancellationToken);

        // Rows exist for emails with no account too, so tidy up ones that have gone quiet now and then; the per-IP
        // limiter on the endpoints already bounds how fast new ones can be made.
        if (Random.Shared.Next(20) == 0)
        {
            var stale = now - TimeSpan.FromDays(1);
            _ = await dbContext.Set<SignInThrottle>().Where(t => t.WindowStartedAt < stale).ExecuteDeleteAsync(cancellationToken);
        }
    }

    public async Task ClearAsync(string emailHash, CancellationToken cancellationToken = default)
    {
        _ = await dbContext.Set<SignInThrottle>().Where(t => t.EmailHash == emailHash).ExecuteDeleteAsync(cancellationToken);
    }
}
