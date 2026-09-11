using Purch.Domain.Common;
using Purch.Domain.Enums;

namespace Purch.Domain.Entities;

public class Transaction : TenantScopedEntity
{
    public Guid BranchId { get; set; }

    public Guid DeviceId { get; set; }

    /// <summary>Sequential per branch/device, server-generated — see ReceiptSequence.</summary>
    public long ReceiptNumber { get; set; }

    public Guid StaffUserId { get; set; }

    public TransactionStatus Status { get; set; } = TransactionStatus.Open;

    public decimal TotalAmount { get; set; }

    public decimal DiscountAmount { get; set; }

    public bool SeniorPwdDiscountApplied { get; set; }

    public string? OrderType { get; set; }

    /// <summary>Kiosk-originated orders are prep-only — set true, payment always finalized at cashier POS.</summary>
    public bool OriginatedFromKiosk { get; set; }
}
