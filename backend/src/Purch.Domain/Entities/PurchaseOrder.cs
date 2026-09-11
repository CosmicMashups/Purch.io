using Purch.Domain.Common;
using Purch.Domain.Enums;

namespace Purch.Domain.Entities;

public class PurchaseOrder : TenantScopedEntity
{
    public Guid SupplierId { get; set; }

    public Guid BranchId { get; set; }

    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;

    public DateTimeOffset? SentAt { get; set; }
}
