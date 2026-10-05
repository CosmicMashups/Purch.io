namespace Purch.Application.Pos;

public sealed record AddTransactionLineRequest(
    Guid ItemId,
    Guid? ItemVariantId,
    decimal Quantity,
    IReadOnlyList<ComboSelectionRequest>? ComboSelections = null,
    IReadOnlyList<Guid>? SelectedModifierIds = null,
    IReadOnlyList<Guid>? SelectedCategoryItemIds = null);

/// <summary>Free on an ordinary cart. On a cart already sent to the kitchen (a claimed kiosk order) whose
/// item hasn't been prepared yet, a Cashier/Warehouse staff member needs an Admin/Manager's
/// <see cref="ApproverPin"/> — an Admin or Manager doesn't, since they're already that approver. Once the
/// kitchen has started preparing it, no PIN helps: the edit is refused outright. See
/// TransactionService.RequireKitchenEditAllowedAsync.</summary>
public sealed record UpdateTransactionLineRequest(decimal Quantity, string? ApproverPin = null);

/// <summary>One picked component for a Combo item's slot (Purch.Domain.Entities.ItemComboComponent).
/// A slot with Quantity N needs N of these carrying the same SlotId.</summary>
public sealed record ComboSelectionRequest(Guid SlotId, Guid SelectedItemId);
