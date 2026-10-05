using Purch.Domain.Common;

namespace Purch.Domain.Entities;

/// <summary>A group's tweak to one item of its linked category: a price charged in place of the item's own
/// (null keeps the item's price) and/or hiding the item from this group. A category item with no row here is
/// offered at its own price.</summary>
public class ModifierGroupCategoryItem : TenantScopedEntity
{
    public Guid ModifierGroupId { get; set; }

    public Guid ItemId { get; set; }

    public decimal? PriceOverride { get; set; }

    public bool IsExcluded { get; set; }
}
