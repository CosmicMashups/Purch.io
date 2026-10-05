using Purch.Domain.Common;

namespace Purch.Domain.Entities;

/// <summary>An inventory item a modifier uses up when it is chosen: Coke Zero takes some syrup and some
/// carbonated water, Extra Cheese takes one more slice. It adds to whatever the item's own recipe already
/// uses and is multiplied by the line's quantity when the sale completes.</summary>
public class ItemModifierIngredient : TenantScopedEntity
{
    public Guid ItemModifierId { get; set; }

    public Guid InventoryItemId { get; set; }

    /// <summary>How much is used each time the modifier is chosen. Null means "only check that it is in stock,
    /// never deduct it", the same convention as <see cref="ItemRecipeLine.QuantityPerOrder"/>.</summary>
    public decimal? QuantityPerOrder { get; set; }
}
