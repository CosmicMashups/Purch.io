namespace Purch.Domain.Enums;

public enum AuditActionType
{
    Void,
    Refund,
    DiscountOverride,
    PriceOverride,
    InventoryAdjustment,
    DepartmentReassignment,
    CreditLimitOverride,
    CashDrawerManualOpen,

    /// <summary>A staff member's role, RBAC scope or branch assignment changed, or they were
    /// deactivated/reactivated — see StaffService.UpdateAsync.</summary>
    StaffAccessChanged,

    /// <summary>An item's selling price changed via the catalog editor — see ItemService.UpdateAsync.
    /// Distinct from PriceOverride, which is a one-off override applied at the point of sale.</summary>
    CatalogPriceChanged,
}
