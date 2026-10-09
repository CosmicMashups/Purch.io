namespace Purch.Domain.Enums;

public enum EquipmentStatus
{
    Operational,

    /// <summary>Still usable, but someone should look at it. Informational only; it never blocks a sale.</summary>
    NeedsRepair,

    /// <summary>Not usable. Every item that needs this equipment shows as out of stock until it is back.</summary>
    OutOfService,
}
