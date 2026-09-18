using Purch.Domain.Common;
using Purch.Domain.Enums;

namespace Purch.Domain.Entities;

public class InventoryMovement : TenantScopedEntity
{
    public Guid ItemId { get; set; }

    /// <summary>Set instead of ItemId when this movement is against a separately tracked InventoryItem rather than a catalog Item.</summary>
    public Guid? InventoryItemId { get; set; }

    public Guid BranchId { get; set; }

    public MovementType Type { get; set; }

    public decimal Quantity { get; set; }

    public Guid StaffUserId { get; set; }

    public string? Note { get; set; }

    public string? ReasonCategory { get; set; }

    public string? PhotoUrl { get; set; }

    public string? SupplierReference { get; set; }
}
