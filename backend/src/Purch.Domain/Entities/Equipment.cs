using Purch.Domain.Common;
using Purch.Domain.Enums;

namespace Purch.Domain.Entities;

/// <summary>A durable thing the tenant owns and needs to keep an eye on, such as an ice cream machine, a deep fryer,
/// a table or a set of utensils. Unlike an InventoryItem it is never used up by a sale; it has a condition, and
/// Items can depend on it through ItemEquipment.</summary>
public class Equipment : TenantScopedEntity, ISoftDeletable
{
    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public Guid? DeletedByUserId { get; set; }

    public string Name { get; set; } = string.Empty;

    public EquipmentKind Kind { get; set; } = EquipmentKind.Equipment;

    public EquipmentStatus Status { get; set; } = EquipmentStatus.Operational;

    /// <summary>How many there are, for things counted in bulk (spoons, chairs). Null for a single tracked asset.</summary>
    public int? Quantity { get; set; }

    public string? Location { get; set; }

    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Position on the Equipment page; lower comes first, ties fall back to name.</summary>
    public int SortOrder { get; set; }
}
