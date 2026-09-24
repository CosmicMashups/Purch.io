namespace Purch.Domain.Enums;

/// <summary>What kind of terminal a paired Device is. Fixed at creation time
/// by the admin in Manage Devices, and checked at pairing time so a leaked
/// pairing code can only ever be used to pair the device type it was
/// generated for (a kiosk code can't be used to pair a kitchen display).</summary>
public enum DeviceType
{
    /// <summary>An attended staff terminal (cashier/admin/manager/warehouse
    /// login via pairing code + staff PIN). No pairing PIN of its own.</summary>
    Register,

    /// <summary>An unattended, customer-facing self-order terminal.</summary>
    Kiosk,

    /// <summary>An unattended display showing pending/ready order numbers to
    /// waiting customers.</summary>
    OrderBoard,

    /// <summary>An unattended display showing pending orders with line items,
    /// for kitchen staff preparing food.</summary>
    KitchenDisplay,

    /// <summary>An unattended device for a warehouse officer, paired with a
    /// pairing PIN. Its session carries the Warehouse role, so it only ever
    /// sees Home and Inventory.</summary>
    WarehouseOfficer,
}
