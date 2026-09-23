using System.IO.Compression;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Retention;

/// <summary>
/// The actual purge logic, separated from the hosted service that schedules it so it can be run directly
/// in a test (or by hand, via a one-off script) without waiting on a timer. Cross-tenant by design — see
/// PurchDbContext's tenant filter doc comment — so every query here opts out with IgnoreQueryFilters().
/// Uses ExecuteDeleteAsync (a single SQL DELETE) rather than loading rows into memory first.
/// When an ArchiveDirectory is configured, audit logs and inventory movements are safely archived
/// to compressed JSON files before deletion. Unreferenced uploaded files older than the grace window
/// are also cleaned up.
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
            if (!string.IsNullOrWhiteSpace(settings.ArchiveDirectory))
            {
                await ArchiveAuditLogsAsync(settings.ArchiveDirectory, cutoff, cancellationToken);
            }

            auditLogsPurged = await dbContext.AuditLogs
                .IgnoreQueryFilters()
                .Where(log => log.CreatedAt < cutoff)
                .ExecuteDeleteAsync(cancellationToken);
        }

        var inventoryMovementsPurged = 0;
        if (settings.InventoryMovementRetentionDays is { } movementDays)
        {
            var cutoff = now.AddDays(-movementDays);
            if (!string.IsNullOrWhiteSpace(settings.ArchiveDirectory))
            {
                await ArchiveInventoryMovementsAsync(settings.ArchiveDirectory, cutoff, cancellationToken);
            }

            inventoryMovementsPurged = await dbContext.InventoryMovements
                .IgnoreQueryFilters()
                .Where(movement => movement.CreatedAt < cutoff)
                .ExecuteDeleteAsync(cancellationToken);
        }

        var orphanedUploadsPurged = 0;
        var uploadsDir = settings.UploadsDirectory ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        if (Directory.Exists(uploadsDir))
        {
            orphanedUploadsPurged = await PurgeOrphanedUploadsAsync(uploadsDir, settings.OrphanedUploadGraceHours, cancellationToken);
        }

        return new RetentionSweepResult(
            refreshTokensPurged,
            passwordResetTokensPurged,
            syncedRecordsPurged,
            auditLogsPurged,
            inventoryMovementsPurged,
            orphanedUploadsPurged);
    }

    private async Task ArchiveAuditLogsAsync(string archiveDirectory, DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        var logs = await dbContext.AuditLogs
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(log => log.CreatedAt < cutoff)
            .ToListAsync(cancellationToken);

        if (logs.Count == 0) return;

        _ = Directory.CreateDirectory(archiveDirectory);
        var fileName = $"audit_logs_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.json.gz";
        var filePath = Path.Combine(archiveDirectory, fileName);

        await using var fileStream = File.Create(filePath);
        await using var gzipStream = new GZipStream(fileStream, CompressionLevel.Optimal);
        await JsonSerializer.SerializeAsync(gzipStream, logs, cancellationToken: cancellationToken);
    }

    private async Task ArchiveInventoryMovementsAsync(string archiveDirectory, DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        var movements = await dbContext.InventoryMovements
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(movement => movement.CreatedAt < cutoff)
            .ToListAsync(cancellationToken);

        if (movements.Count == 0) return;

        _ = Directory.CreateDirectory(archiveDirectory);
        var fileName = $"inventory_movements_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.json.gz";
        var filePath = Path.Combine(archiveDirectory, fileName);

        await using var fileStream = File.Create(filePath);
        await using var gzipStream = new GZipStream(fileStream, CompressionLevel.Optimal);
        await JsonSerializer.SerializeAsync(gzipStream, movements, cancellationToken: cancellationToken);
    }

    private async Task<int> PurgeOrphanedUploadsAsync(string uploadsDir, int graceHours, CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddHours(-Math.Max(1, graceHours));
        var candidateFiles = Directory.GetFiles(uploadsDir, "*.*", SearchOption.AllDirectories)
            .Where(file => File.GetLastWriteTimeUtc(file) < cutoff)
            .ToList();

        if (candidateFiles.Count == 0) return 0;

        var itemImages = await dbContext.Items.IgnoreQueryFilters().Where(i => i.ImageUrl != null).Select(i => i.ImageUrl!).ToListAsync(cancellationToken);
        var categoryImages = await dbContext.Categories.IgnoreQueryFilters().Where(c => c.ImageUrl != null).Select(c => c.ImageUrl!).ToListAsync(cancellationToken);
        var branchQrs = await dbContext.Branches.IgnoreQueryFilters().Where(b => b.ManualGcashQrImageUrl != null).Select(b => b.ManualGcashQrImageUrl!).ToListAsync(cancellationToken);
        var tenantPosters = await dbContext.Tenants.IgnoreQueryFilters().Where(t => t.KioskPosterImageUrl != null).Select(t => t.KioskPosterImageUrl!).ToListAsync(cancellationToken);

        var referencedNames = new HashSet<string>(
            itemImages.Concat(categoryImages).Concat(branchQrs).Concat(tenantPosters)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrEmpty(name))!,
            StringComparer.OrdinalIgnoreCase);

        var deletedCount = 0;
        foreach (var file in candidateFiles)
        {
            var fileName = Path.GetFileName(file);
            if (!referencedNames.Contains(fileName))
            {
                try
                {
                    File.Delete(file);
                    deletedCount++;
                }
                catch
                {
                    // Ignore transient file lock
                }
            }
        }

        return deletedCount;
    }
}
