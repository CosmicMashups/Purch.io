using Purch.Domain.Common;

namespace Purch.Domain.Entities;

/// <summary>One ingredient line in an Item's recipe — how much of a given
/// InventoryItem is consumed per order of that Item, when the tenant has
/// UseSeparateInventoryTracking enabled.</summary>
public class ItemRecipeLine : TenantScopedEntity
{
    public Guid ItemId { get; set; }

    public Guid InventoryItemId { get; set; }

    public decimal? QuantityPerOrder { get; set; }
}
