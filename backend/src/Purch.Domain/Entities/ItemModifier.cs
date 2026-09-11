using Purch.Domain.Common;

namespace Purch.Domain.Entities;

public class ItemModifier : TenantScopedEntity
{
    public Guid ModifierGroupId { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal PriceDelta { get; set; }
}
