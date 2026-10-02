namespace Purch.Domain.Enums;

/// <summary>What a staff member is qualified to work as. A device type maps to one duty (a Register is Cashier,
/// a Warehouse device is Warehouse); signing in on a device needs the matching duty. Admin and Manager are
/// qualified for everything regardless of this value.</summary>
[Flags]
public enum StaffDuty
{
    None = 0,
    Cashier = 1,
    Warehouse = 2,
    Kitchen = 4,
}
