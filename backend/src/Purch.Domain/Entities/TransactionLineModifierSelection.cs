using Purch.Domain.Common;

namespace Purch.Domain.Entities;

/// <summary>One chosen option on a cart line — mirrors TransactionLineComboSelection's join-row shape. It is
/// either a modifier (e.g. "No Ice", whose group is reachable via ItemModifier.ModifierGroupId) or, for a group
/// linked to a category, one of that category's items. Exactly one of ItemModifierId / ItemId is set.</summary>
public class TransactionLineModifierSelection : TenantScopedEntity
{
    public Guid TransactionLineId { get; set; }

    public Guid? ItemModifierId { get; set; }

    /// <summary>The category item chosen through a category-linked group.</summary>
    public Guid? ItemId { get; set; }

    /// <summary>The group a category item was chosen through (needed to show the group name on a receipt).</summary>
    public Guid? ModifierGroupId { get; set; }

    /// <summary>The price actually charged for a category item, frozen at sale time. Null for plain modifiers.</summary>
    public decimal? PriceCharged { get; set; }
}
