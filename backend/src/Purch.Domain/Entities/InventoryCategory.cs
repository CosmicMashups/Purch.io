using Purch.Domain.Common;

namespace Purch.Domain.Entities;

/// <summary>A group for ingredients (InventoryItem), kept apart from the item Category so the till and kiosk never see it.</summary>
public class InventoryCategory : TenantScopedEntity
{
    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }
}
