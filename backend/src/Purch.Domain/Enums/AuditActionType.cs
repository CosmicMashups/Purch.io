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
}
