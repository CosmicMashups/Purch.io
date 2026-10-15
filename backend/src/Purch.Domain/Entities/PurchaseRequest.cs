using Purch.Domain.Common;
using Purch.Domain.Enums;

namespace Purch.Domain.Entities;

/// <summary>What the shop needs to restock, before a supplier or a price is chosen. An approved request is converted into one
/// draft purchase order per supplier.</summary>
public class PurchaseRequest : TenantScopedEntity
{
    public Guid BranchId { get; set; }

    /// <summary>Per-branch series, e.g. MAIN-PR-00001. Never reused.</summary>
    public string ControlNumber { get; set; } = string.Empty;

    public DateOnly RequestDate { get; set; }

    public DateOnly? DateNeeded { get; set; }

    public Guid RequestedByUserId { get; set; }

    public string RequestedByName { get; set; } = string.Empty;

    /// <summary>Frozen at creation: the branch address, or the registered address for a one-branch business.</summary>
    public string? DeliveryAddress { get; set; }

    public string? Remarks { get; set; }

    public PurchaseRequestStatus Status { get; set; } = PurchaseRequestStatus.Open;

    public Guid? ApprovedByUserId { get; set; }

    public DateTimeOffset? ApprovedAt { get; set; }
}

public class PurchaseRequestLine : TenantScopedEntity
{
    public Guid PurchaseRequestId { get; set; }

    /// <summary>Empty when the line orders an ingredient with no catalog item.</summary>
    public Guid ItemId { get; set; }

    public Guid? InventoryItemId { get; set; }

    public decimal Quantity { get; set; }

    /// <summary>UOM text, e.g. pc, kg, box.</summary>
    public string Unit { get; set; } = "pc";
}

/// <summary>A supplier an item or ingredient is normally bought from. One link per item is preferred.</summary>
public class ItemSupplier : TenantScopedEntity
{
    public Guid? ItemId { get; set; }

    public Guid? InventoryItemId { get; set; }

    public Guid SupplierId { get; set; }

    public bool IsPreferred { get; set; }

    public decimal? UnitCost { get; set; }
}

/// <summary>Gap-free counter behind control numbers, one row per branch and document kind.</summary>
public class DocumentSequence : TenantScopedEntity
{
    public Guid BranchId { get; set; }

    /// <summary>PR or PO.</summary>
    public string Kind { get; set; } = string.Empty;

    public long LastNumber { get; set; }
}
