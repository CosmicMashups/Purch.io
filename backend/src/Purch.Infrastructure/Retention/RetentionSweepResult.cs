namespace Purch.Infrastructure.Retention;

public sealed record RetentionSweepResult(
    int RefreshTokensPurged,
    int PasswordResetTokensPurged,
    int SyncedRecordsPurged,
    int AuditLogsPurged,
    int InventoryMovementsPurged,
    int OrphanedUploadsPurged = 0,
    int CartBatchReceiptsPurged = 0)
{
    public int Total => RefreshTokensPurged + PasswordResetTokensPurged + SyncedRecordsPurged
        + AuditLogsPurged + InventoryMovementsPurged + OrphanedUploadsPurged + CartBatchReceiptsPurged;
}
