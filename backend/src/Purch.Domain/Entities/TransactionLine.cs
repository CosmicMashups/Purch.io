using Purch.Domain.Common;

namespace Purch.Domain.Entities;

public class TransactionLine : TenantScopedEntity
{
    public Guid TransactionId { get; set; }

    public Guid ItemId { get; set; }

    public Guid? ItemVariantId { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal LineTotal { get; set; }
}
