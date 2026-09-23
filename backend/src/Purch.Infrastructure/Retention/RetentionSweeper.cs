using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Retention;

/// <summary>
/// The actual purge logic, separated from the hosted service that schedules it so it can be run directly
/// in a test (or by hand, via a one-off script) without waiting on a timer. Cross-tenant by design — see
/// PurchDbContext's tenant filter doc comment — so every query here opts out with IgnoreQueryFilters().
/// Uses ExecuteDeleteAsync (a single SQL DELETE) rather than loading rows into memory first.
/// </summary>
public sealed class RetentionSweeper(PurchDbContext dbContext, IOptions<RetentionOptions> options)
{
    public async Task<RetentionSweepResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var settings = options.Value;

        var tokenCutoff = now.AddDays(-settings.ExpiredTokenGraceDays);

        var refreshTokensPurged = await dbContext.RefreshTokens
            .IgnoreQueryFilters()
            .Where(token => (token.RevokedAt != null && token.RevokedAt < tokenCutoff)
                || (token.RevokedAt == null && token.ExpiresAt < tokenCutoff))
            .ExecuteDeleteAsync(cancellationToken);

        var passwordResetTokensPurged = await dbContext.PasswordResetTokens
            .IgnoreQueryFilters()
            .Where(token => (token.UsedAt != null && token.UsedAt < tokenCutoff)
                || (token.UsedAt == null && token.ExpiresAt < tokenCutoff))
            .ExecuteDeleteAsync(cancellationToken);

        var syncedRecordCutoff = now.AddDays(-settings.SyncedRecordRetentionDays);
        var syncedRecordsPurged = await dbContext.SyncedRecords
            .IgnoreQueryFilters()
            // A record still flagged for review is kept regardless of age — see SyncedRecord.FlaggedForReview.
            .Where(record => !record.FlaggedForReview && record.CreatedAt < syncedRecordCutoff)
            .ExecuteDeleteAsync(cancellationToken);

        var auditLogsPurged = 0;
        if (settings.AuditLogRetentionDays is { } auditLogDays)
        {
            var cutoff = now.AddDays(-auditLogDays);
            auditLogsPurged = await dbContext.AuditLogs
                .IgnoreQueryFilters()
                .Where(log => log.CreatedAt < cutoff)
                .ExecuteDeleteAsync(cancellationToken);
        }

        var inventoryMovementsPurged = 0;
        if (settings.InventoryMovementRetentionDays is { } movementDays)
        {
            var cutoff = now.AddDays(-movementDays);
            inventoryMovementsPurged = await dbContext.InventoryMovements
                .IgnoreQueryFilters()
                .Where(movement => movement.CreatedAt < cutoff)
                .ExecuteDeleteAsync(cancellationToken);
        }

        return new RetentionSweepResult(
            refreshTokensPurged,
            passwordResetTokensPurged,
            syncedRecordsPurged,
            auditLogsPurged,
            inventoryMovementsPurged);
    }
}
