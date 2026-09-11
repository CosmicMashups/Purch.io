using Purch.Domain.Enums;

namespace Purch.Application.Pos;

public sealed record TransactionDto(
    Guid Id,
    Guid BranchId,
    Guid DeviceId,
    TransactionStatus Status,
    IReadOnlyList<TransactionLineDto> Lines,
    decimal Subtotal,
    decimal DiscountAmount,
    bool SeniorPwdDiscountApplied,
    decimal TotalAmount,
    long? ReceiptNumber,
    IReadOnlyList<PaymentDto> Payments);

public sealed record TransactionLineDto(
    Guid Id,
    Guid ItemId,
    string ItemName,
    Guid? ItemVariantId,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    IReadOnlyList<ComboSelectionDto> ComboSelections);

/// <summary>Mirrors Purch.Domain.Entities.TransactionLineComboSelection, resolved to
/// readable slot/item names for receipt display.</summary>
public sealed record ComboSelectionDto(
    Guid SlotId,
    string SlotLabel,
    Guid SelectedItemId,
    string SelectedItemName);
