using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.EquipmentInventory;

/// <summary>Works out which items cannot be made because equipment they need is out of service. Pure, so the
/// catalog listing can batch it and the sale path can reuse the same rule.</summary>
public static class EquipmentAvailability
{
    /// <summary>True when the equipment stops the items that need it from being sold. Retired equipment (inactive or
    /// deleted) is no longer a dependency, and NeedsRepair is only a warning.</summary>
    public static bool IsBlocking(Equipment equipment)
    {
        return equipment is { IsActive: true, IsDeleted: false, Status: EquipmentStatus.OutOfService };
    }

    public static HashSet<Guid> BlockedItemIds(IEnumerable<ItemEquipment> links, IReadOnlyDictionary<Guid, Equipment> equipmentById)
    {
        return [.. links
            .Where(link => equipmentById.TryGetValue(link.EquipmentId, out var equipment) && IsBlocking(equipment))
            .Select(link => link.ItemId)];
    }
}
