using Purch.Domain.Common;

namespace Purch.Domain.Entities;

/// <summary>
/// One row per (TenantId, BranchId) — deliberately branch-wide, not per-kiosk-device
/// like ReceiptSequence: several kiosk terminals at one branch should share a single
/// prep-number series so a cashier calling out "order 42" is unambiguous branch-wide.
/// </summary>
public class KioskPrepSequence : TenantScopedEntity
{
    public Guid BranchId { get; set; }

    public long LastIssuedNumber { get; set; }
}
