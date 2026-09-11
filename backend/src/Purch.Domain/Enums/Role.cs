namespace Purch.Domain.Enums;

public enum Role
{
    Admin,
    Manager,
    Cashier,
    Warehouse,

    /// <summary>A paired kiosk terminal, not a staff member — its JWT carries no
    /// user/sub claim. Authorized for order-building endpoints only (see
    /// KioskEndpoints); never included in any payment/discount/promo/utang
    /// endpoint's allowed-roles list, which is what actually enforces "no
    /// payment from the kiosk" (see docs plan's Phase 7 key decision).</summary>
    Kiosk,
}
