namespace Purch.Application.Pos;

public sealed record AddTransactionLineRequest(
    Guid ItemId,
    Guid? ItemVariantId,
    decimal Quantity,
    IReadOnlyList<ComboSelectionRequest>? ComboSelections = null,
    IReadOnlyList<Guid>? SelectedModifierIds = null);

public sealed record UpdateTransactionLineRequest(decimal Quantity);

/// <summary>One picked component for a Combo item's slot (Purch.Domain.Entities.ItemComboComponent).
/// A slot with Quantity N needs N of these carrying the same SlotId.</summary>
public sealed record ComboSelectionRequest(Guid SlotId, Guid SelectedItemId);
