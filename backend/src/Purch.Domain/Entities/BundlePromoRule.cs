using Purch.Domain.Common;

namespace Purch.Domain.Entities;

/// <summary>Multi-buy rules, e.g. "Buy 2 Get 1", "3 for ₱X".</summary>
public class BundlePromoRule : TenantScopedEntity
{
    public Guid ItemId { get; set; }

    public string Description { get; set; } = string.Empty;

    public int TriggerQuantity { get; set; }

    public decimal BundlePrice { get; set; }

    public bool IsActive { get; set; } = true;
}
