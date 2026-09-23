using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Purch.Application.Common;
using Purch.Domain.Entities;
using Purch.Infrastructure.Persistence;
using Purch.Infrastructure.Retention;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class RetentionSweeperTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task An_expired_refresh_token_is_purged_only_once_the_grace_period_has_fully_elapsed()
    {
        var now = DateTimeOffset.UtcNow;
        await using var dbContext = OpenDbContext();

        var stillInGrace = new RefreshToken { TenantId = Guid.NewGuid(), TokenHash = "a", ExpiresAt = now.AddDays(-3) };
        var pastGrace = new RefreshToken { TenantId = Guid.NewGuid(), TokenHash = "b", ExpiresAt = now.AddDays(-10) };
        var stillValid = new RefreshToken { TenantId = Guid.NewGuid(), TokenHash = "c", ExpiresAt = now.AddDays(10) };
        dbContext.RefreshTokens.AddRange(stillInGrace, pastGrace, stillValid);
        _ = await dbContext.SaveChangesAsync();

        var ourHashes = new[] { "a", "b", "c" };
        var beforeCount = await dbContext.RefreshTokens.IgnoreQueryFilters().Where(t => ourHashes.Contains(t.TokenHash)).CountAsync();

        var result = await Sweeper(dbContext, new RetentionOptions { ExpiredTokenGraceDays = 7 }).RunAsync();

        // The shared test database also holds real refresh tokens from every other test's login flow, none of
        // which are old enough to be purged, so RefreshTokensPurged is checked as "at least our one row",
        // not an exact total; "remaining" is scoped to our own rows so an unrelated token can't hide a miss.
        Assert.True(result.RefreshTokensPurged >= 1);
        Assert.Equal(beforeCount - 1, await dbContext.RefreshTokens.IgnoreQueryFilters().Where(t => ourHashes.Contains(t.TokenHash)).CountAsync());
        var remaining = await dbContext.RefreshTokens.IgnoreQueryFilters().Where(t => ourHashes.Contains(t.TokenHash)).Select(t => t.TokenHash).ToListAsync();
        Assert.Equal(["a", "c"], remaining.OrderBy(x => x));
    }

    [Fact]
    public async Task A_revoked_refresh_token_is_purged_by_when_it_was_revoked_not_when_it_expires()
    {
        var now = DateTimeOffset.UtcNow;
        await using var dbContext = OpenDbContext();

        // Expires far in the future, but was revoked long ago — revocation, not the original expiry, governs.
        var revokedLongAgo = new RefreshToken
        {
            TenantId = Guid.NewGuid(),
            TokenHash = "revoked",
            ExpiresAt = now.AddDays(20),
            RevokedAt = now.AddDays(-10),
        };
        _ = dbContext.RefreshTokens.Add(revokedLongAgo);
        _ = await dbContext.SaveChangesAsync();

        var result = await Sweeper(dbContext, new RetentionOptions { ExpiredTokenGraceDays = 7 }).RunAsync();

        Assert.True(result.RefreshTokensPurged >= 1);
        Assert.False(await dbContext.RefreshTokens.IgnoreQueryFilters().AnyAsync(t => t.Id == revokedLongAgo.Id));
    }

    [Fact]
    public async Task A_used_password_reset_token_is_purged_after_the_grace_period()
    {
        var now = DateTimeOffset.UtcNow;
        await using var dbContext = OpenDbContext();

        var usedLongAgo = new PasswordResetToken
        {
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TokenHash = "used",
            ExpiresAt = now.AddHours(-1),
            UsedAt = now.AddDays(-10),
        };
        var neverUsedButExpired = new PasswordResetToken
        {
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TokenHash = "expired",
            ExpiresAt = now.AddDays(-10),
        };
        dbContext.PasswordResetTokens.AddRange(usedLongAgo, neverUsedButExpired);
        _ = await dbContext.SaveChangesAsync();

        var result = await Sweeper(dbContext, new RetentionOptions { ExpiredTokenGraceDays = 7 }).RunAsync();

        Assert.True(result.PasswordResetTokensPurged >= 2);
        Assert.False(await dbContext.PasswordResetTokens.IgnoreQueryFilters().AnyAsync(t => t.Id == usedLongAgo.Id));
        Assert.False(await dbContext.PasswordResetTokens.IgnoreQueryFilters().AnyAsync(t => t.Id == neverUsedButExpired.Id));
    }

    [Fact]
    public async Task A_synced_record_flagged_for_review_is_kept_no_matter_its_age()
    {
        var now = DateTimeOffset.UtcNow;
        await using var dbContext = OpenDbContext();

        var old = new SyncedRecord
        {
            TenantId = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            IdempotencyKey = "old",
            EntityType = "Transaction",
            EntityId = Guid.NewGuid(),
            ClientTimestamp = now.AddDays(-60),
            CreatedAt = now.AddDays(-60),
        };
        var oldButFlagged = new SyncedRecord
        {
            TenantId = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            IdempotencyKey = "flagged",
            EntityType = "Transaction",
            EntityId = Guid.NewGuid(),
            ClientTimestamp = now.AddDays(-60),
            CreatedAt = now.AddDays(-60),
            FlaggedForReview = true,
        };
        dbContext.AddRange(old, oldButFlagged);
        _ = await dbContext.SaveChangesAsync();

        var result = await Sweeper(dbContext, new RetentionOptions { SyncedRecordRetentionDays = 30 }).RunAsync();

        Assert.True(result.SyncedRecordsPurged >= 1);
        Assert.False(await dbContext.SyncedRecords.IgnoreQueryFilters().AnyAsync(r => r.Id == old.Id));
        Assert.True(await dbContext.SyncedRecords.IgnoreQueryFilters().AnyAsync(r => r.Id == oldButFlagged.Id));
    }

    [Fact]
    public async Task Audit_logs_and_inventory_movements_are_left_untouched_when_no_retention_period_is_configured()
    {
        var now = DateTimeOffset.UtcNow;
        await using var dbContext = OpenDbContext();

        var auditLog = new AuditLog
        {
            TenantId = Guid.NewGuid(),
            ActorUserId = Guid.NewGuid(),
            TargetEntityType = "Transaction",
            TargetEntityId = Guid.NewGuid(),
            CreatedAt = now.AddYears(-5),
        };
        var movement = new InventoryMovement
        {
            TenantId = Guid.NewGuid(),
            ItemId = Guid.NewGuid(),
            BranchId = Guid.NewGuid(),
            StaffUserId = Guid.NewGuid(),
            Quantity = 1,
            CreatedAt = now.AddYears(-5),
        };
        _ = dbContext.AuditLogs.Add(auditLog);
        _ = dbContext.InventoryMovements.Add(movement);
        _ = await dbContext.SaveChangesAsync();

        var result = await Sweeper(dbContext, new RetentionOptions()).RunAsync();

        Assert.Equal(0, result.AuditLogsPurged);
        Assert.Equal(0, result.InventoryMovementsPurged);
        Assert.True(await dbContext.AuditLogs.IgnoreQueryFilters().AnyAsync(a => a.Id == auditLog.Id));
        Assert.True(await dbContext.InventoryMovements.IgnoreQueryFilters().AnyAsync(m => m.Id == movement.Id));
    }

    [Fact]
    public async Task Audit_logs_are_purged_once_a_retention_period_is_explicitly_configured()
    {
        var now = DateTimeOffset.UtcNow;
        await using var dbContext = OpenDbContext();
        var auditLog = new AuditLog
        {
            TenantId = Guid.NewGuid(),
            ActorUserId = Guid.NewGuid(),
            TargetEntityType = "Transaction",
            TargetEntityId = Guid.NewGuid(),
            CreatedAt = now.AddDays(-800),
        };
        _ = dbContext.AuditLogs.Add(auditLog);
        _ = await dbContext.SaveChangesAsync();

        var result = await Sweeper(dbContext, new RetentionOptions { AuditLogRetentionDays = 730 }).RunAsync();

        Assert.True(result.AuditLogsPurged >= 1);
        Assert.False(await dbContext.AuditLogs.IgnoreQueryFilters().AnyAsync(a => a.Id == auditLog.Id));
    }

    private PurchDbContext OpenDbContext()
    {
        var options = new DbContextOptionsBuilder<PurchDbContext>().UseNpgsql(postgres.ConnectionString).Options;
        return new PurchDbContext(options, new NoTenant());
    }

    private static RetentionSweeper Sweeper(PurchDbContext dbContext, RetentionOptions settings)
    {
        return new RetentionSweeper(dbContext, Options.Create(settings));
    }

    private sealed class NoTenant : ICurrentTenantProvider
    {
        public Guid? TenantId => null;
    }
}
