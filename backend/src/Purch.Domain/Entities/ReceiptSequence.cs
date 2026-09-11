using Purch.Domain.Common;

namespace Purch.Domain.Entities;

/// <summary>
/// One row per (TenantId, BranchId, DeviceId) — BIR requires sequential, gap-auditable
/// numbering per Machine Identification Number, not one company-wide series.
/// Incremented atomically (SELECT ... FOR UPDATE) inside the transaction-create use case.
/// </summary>
public class ReceiptSequence : TenantScopedEntity
{
    public Guid BranchId { get; set; }

    public Guid DeviceId { get; set; }

    public long LastIssuedNumber { get; set; }
}
