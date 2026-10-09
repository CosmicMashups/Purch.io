using Purch.Domain.Common;

namespace Purch.Domain.Entities;

/// <summary>An Item needs this Equipment to be made. While the equipment is out of service the item cannot be sold.</summary>
public class ItemEquipment : TenantScopedEntity
{
    public Guid ItemId { get; set; }

    public Guid EquipmentId { get; set; }
}
