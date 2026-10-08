using Purch.Domain.Common;

namespace Purch.Domain.Entities;

public class ItemModifier : TenantScopedEntity, ISoftDeletable
{
    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public Guid? DeletedByUserId { get; set; }

    public Guid ModifierGroupId { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal PriceDelta { get; set; }
}
