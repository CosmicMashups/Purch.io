using Purch.Domain.Entities;

namespace Purch.Application.Pos;

/// <summary>
/// One row per (TenantId, BranchId, DeviceId) — BIR requires sequential,
/// gap-auditable numbering per Machine Identification Number, not one
/// company-wide series.
/// </summary>
public interface IReceiptSequenceRepository
{
    /// <summary>Returns the tracked row for this device, creating one (LastIssuedNumber 0) if it doesn't exist yet. Caller increments and saves — kept in the same SaveChangesAsync as the payment/transaction update so the number is never burned on a rollback.</summary>
    Task<ReceiptSequence> GetOrCreateTrackedAsync(Guid tenantId, Guid branchId, Guid deviceId, CancellationToken cancellationToken = default);
}
