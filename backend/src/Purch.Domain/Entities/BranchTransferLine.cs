using Purch.Domain.Common;

namespace Purch.Domain.Entities;

public class BranchTransferLine : TenantScopedEntity
{
    public Guid BranchTransferId { get; set; }

    public Guid ItemId { get; set; }

    public decimal Quantity { get; set; }
}
