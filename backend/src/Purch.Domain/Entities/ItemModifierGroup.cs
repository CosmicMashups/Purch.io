using Purch.Domain.Common;

namespace Purch.Domain.Entities;

/// <summary>Join row: which modifier groups (e.g. "Ice Level", "No Pickles") apply to which item. Many-to-many so a group is reusable across items.</summary>
public class ItemModifierGroup : TenantScopedEntity
{
    public Guid ItemId { get; set; }

    public Guid ModifierGroupId { get; set; }
}
