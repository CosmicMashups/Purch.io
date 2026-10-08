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

    /// <summary>A customer's personal data was anonymized/erased under the Data Privacy Act (RA 10173) / GDPR.</summary>
    CustomerAnonymized,

    /// <summary>An exchange (return plus replacement) was recorded against a completed sale — see
    /// AdjustmentService. The original sale's own audit trail is untouched; this is the adjustment's own entry.</summary>
    Exchange,

    /// <summary>A line was changed or removed on a claimed kiosk order the kitchen hadn't started yet, by a
    /// Cashier or Warehouse staff member who needed a different Admin/Manager's PIN to do it — see
    /// TransactionService.RequireKitchenEditAllowedAsync. Not recorded for the free-edit case (an
    /// Admin/Manager editing it themselves needs no approval, so there's nothing unusual to log).</summary>
    KitchenOrderLineEdited,

    /// <summary>A repayment against a customer's utang balance, recorded by whoever collected it — see
    /// CustomerCreditLedgerService.RecordPaymentAsync. Any POS role can collect, so who did it and how much
    /// is the only control against a balance being quietly written off.</summary>
    CreditPaymentRecorded,

    /// <summary>A record was switched to Inactive. TargetEntityType names what kind.</summary>
    RecordDeactivated,

    /// <summary>An Inactive record was switched back to Active.</summary>
    RecordReactivated,

    /// <summary>A record was soft-deleted. The row stays in the database and can be restored.</summary>
    RecordDeleted,

    /// <summary>A soft-deleted record was restored.</summary>
    RecordRestored,
}
