namespace Purch.Infrastructure.Retention;

/// <summary>
/// Bound from the "Retention" config section. Refresh tokens, password reset tokens and synced-record
/// idempotency rows are safe to purge once stale — the defaults below do that. Audit logs and inventory
/// movements can carry legal/BIR significance, so their retention is opt-in (null = keep forever) rather
/// than defaulted. When an archive directory is specified, rows are exported to a compressed JSON archive
/// before deletion.
/// </summary>
public sealed class RetentionOptions
{
    public const string SectionName = "Retention";

    /// <summary>How often the background sweep runs.</summary>
    public int IntervalHours { get; set; } = 6;

    /// <summary>A refresh or password-reset token is purged this many days after it expired, was revoked,
    /// or (for a reset token) was used — not the instant it becomes unusable, so a brief clock skew between
    /// this job and the token's own expiry check can never race a legitimate use.</summary>
    public int ExpiredTokenGraceDays { get; set; } = 7;

    /// <summary>A synced-record idempotency row is purged this many days after the client recorded it,
    /// provided it isn't sitting flagged for review. Long enough that a terminal retrying a stale batch
    /// still dedupes correctly; short enough not to grow forever.</summary>
    public int SyncedRecordRetentionDays { get; set; } = 30;

    /// <summary>Null (the default) keeps every audit log row forever. Set a value to purge rows older
    /// than that many days.</summary>
    public int? AuditLogRetentionDays { get; set; }

    /// <summary>Null (the default) keeps every inventory movement row forever. Set a value to purge rows
    /// older than that many days.</summary>
    public int? InventoryMovementRetentionDays { get; set; }

    /// <summary>Optional directory where purged audit logs and inventory movements are archived as compressed
    /// JSON (.json.gz) before deletion. Null disables archival.</summary>
    public string? ArchiveDirectory { get; set; }

    /// <summary>Optional path to uploads directory for sweeping unreferenced images. Null uses default wwwroot/uploads.</summary>
    public string? UploadsDirectory { get; set; }

    /// <summary>How old an unreferenced file must be (in hours) before it is eligible for deletion (default 24h).</summary>
    public int OrphanedUploadGraceHours { get; set; } = 24;
}
