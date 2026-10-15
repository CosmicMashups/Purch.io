using Purch.Domain.Common;

namespace Purch.Domain.Entities;

/// <summary>An Incoming Receiving Report (IRR): what actually arrived from a supplier, and who took it in.
/// It may exist before its purchase order does; an admin links the two later.</summary>
public class IncomingReceivingReport : TenantScopedEntity
{
    /// <summary>The report's own number, for example MAIN-IRR-00001; blank on reports saved before numbering.</summary>
    public string ControlNumber { get; set; } = string.Empty;

    public Guid? PurchaseOrderId { get; set; }

    public Guid SupplierId { get; set; }

    public Guid BranchId { get; set; }

    public Guid ReceivedByUserId { get; set; }

    public DateOnly DeliveryDate { get; set; }

    public string? Remarks { get; set; }

    /// <summary>True once the accepted quantities have been counted against the purchase order's lines.</summary>
    public bool AppliedToPurchaseOrder { get; set; }
}
