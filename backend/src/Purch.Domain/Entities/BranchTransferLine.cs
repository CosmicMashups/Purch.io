using Purch.Domain.Common;

namespace Purch.Domain.Entities;

public class BranchTransferLine : TenantScopedEntity
{
    public Guid BranchTransferId { get; set; }

    /// <summary>The catalog Item, or null when the line moves a standalone ingredient (InventoryItemId).</summary>
    public Guid? ItemId { get; set; }

    public Guid? InventoryItemId { get; set; }

    public decimal Quantity { get; set; }
}
