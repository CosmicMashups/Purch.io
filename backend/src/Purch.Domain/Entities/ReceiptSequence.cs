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

    /// <summary>BIR Z-reading's "Grand Accumulated Sales" — ever-increasing, never reset, across this device's whole lifetime.</summary>
    public decimal GrandAccumulatedSales { get; set; }

    /// <summary>BIR Z-reading's "Reset Counter" — increments once per Z-reading run on this device.</summary>
    public int ZReadingResetCounter { get; set; }

    /// <summary>The highest ReceiptNumber covered by the last Z-reading — the next one starts right after.</summary>
    public long LastZReadingReceiptNumber { get; set; }

    public DateTimeOffset? LastZReadingAt { get; set; }
}
