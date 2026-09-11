using Purch.Domain.Common;

namespace Purch.Domain.Entities;

public class PurchaseOrderLine : TenantScopedEntity
{
    public Guid PurchaseOrderId { get; set; }

    public Guid ItemId { get; set; }

    public decimal QuantityOrdered { get; set; }

    public decimal QuantityReceived { get; set; }

    public decimal ExpectedUnitCost { get; set; }
}
