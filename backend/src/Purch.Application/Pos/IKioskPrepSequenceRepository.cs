using Purch.Domain.Entities;

namespace Purch.Application.Pos;

/// <summary>One row per (TenantId, BranchId) — see KioskPrepSequence.</summary>
public interface IKioskPrepSequenceRepository
{
    /// <summary>Returns the tracked row for this branch, creating one (LastIssuedNumber 0) if it doesn't exist yet. Caller increments and saves in the same SaveChangesAsync as the order submission, same pattern as IReceiptSequenceRepository.</summary>
    Task<KioskPrepSequence> GetOrCreateTrackedAsync(Guid tenantId, Guid branchId, CancellationToken cancellationToken = default);
}
