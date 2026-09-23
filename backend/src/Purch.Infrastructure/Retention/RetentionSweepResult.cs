namespace Purch.Infrastructure.Retention;

public sealed record RetentionSweepResult(
    int RefreshTokensPurged,
    int PasswordResetTokensPurged,
    int SyncedRecordsPurged,
    int AuditLogsPurged,
    int InventoryMovementsPurged)
{
    public int Total => RefreshTokensPurged + PasswordResetTokensPurged + SyncedRecordsPurged
        + AuditLogsPurged + InventoryMovementsPurged;
}
