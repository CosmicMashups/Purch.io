using Purch.Domain.Common;

namespace Purch.Domain.Entities;

public class ItemComboComponent : TenantScopedEntity
{
    public Guid ParentItemId { get; set; }

    /// <summary>The category the customer picks from. For a fixed slot this is that item's own category,
    /// kept so everything that already reads a slot's category keeps working.</summary>
    public Guid ComponentCategoryId { get; set; }

    /// <summary>When set, the slot is not a choice: every unit of the slot is exactly this item
    /// ("2 pcs Fried Chicken" in a Buy 1 Take 1). The server fills the selections in itself.</summary>
    public Guid? ComponentItemId { get; set; }

    public string SlotLabel { get; set; } = string.Empty;

    public int Quantity { get; set; } = 1;

    /// <summary>A flat surcharge added once for the slot whichever item is picked, as it always has been.</summary>
    public decimal? SubstitutionUpchargeAmount { get; set; }

    /// <summary>Per-choice surcharges for a "choose" slot: a JSON object of item id to extra price. An item
    /// that is not listed is included in the combo price. Null when no choice costs extra.</summary>
    public string? ChoiceUpchargesJson { get; set; }
}
