using Purch.Domain.Common;

namespace Purch.Domain.Entities;

/// <summary>A separately tracked stock unit (an ingredient/raw material or a
/// packaged good) used when a tenant opts into UseSeparateInventoryTracking.
/// May stand alone (managed directly) or be auto-created and paired 1:1 with
/// an Item via LinkedItemId when the tenant has no recipe defined for it.</summary>
public class InventoryItem : TenantScopedEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Sku { get; set; }

    public string BaseUnit { get; set; } = "pc";

    public string PackagingUnit { get; set; } = "pc";

    public decimal PackagingSize { get; set; } = 1;

    public decimal QuantityOnHand { get; set; }

    public decimal? LowStockThreshold { get; set; }

    /// <summary>True when this record was created automatically alongside an Item (see LinkedItemId) rather than by a user.</summary>
    public bool IsAutoCreatedForItem { get; set; }

    /// <summary>Set when this InventoryItem is the auto-created 1:1 stock record for an Item that has no explicit recipe.</summary>
    public Guid? LinkedItemId { get; set; }

    public bool IsActive { get; set; } = true;
}
