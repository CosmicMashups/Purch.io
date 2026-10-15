using Purch.Domain.Common;
using Purch.Domain.Enums;

namespace Purch.Domain.Entities;

public class PurchaseOrder : TenantScopedEntity
{
    public Guid SupplierId { get; set; }

    public Guid BranchId { get; set; }

    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;

    public DateTimeOffset? SentAt { get; set; }

    public string ControlNumber { get; set; } = string.Empty;

    public Guid? PurchaseRequestId { get; set; }

    public DateOnly RequestDate { get; set; }

    public DateOnly? DateNeeded { get; set; }

    public string RequestedByName { get; set; } = string.Empty;

    public string? DeliveryAddress { get; set; }

    public string? Remarks { get; set; }

    /// <summary>The supplier's payment account at the time of ordering.</summary>
    public string? PaymentAccountName { get; set; }

    public string? PaymentAccountNumber { get; set; }

    public bool VatInclusive { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal VatRate { get; set; }

    public decimal Subtotal { get; set; }

    public decimal VatAmount { get; set; }

    public decimal OrderTotal { get; set; }
}
