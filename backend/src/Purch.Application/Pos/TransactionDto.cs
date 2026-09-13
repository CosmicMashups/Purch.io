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
    string? PromoCode,
    decimal PromoDiscountAmount,
    decimal TotalAmount,
    long? ReceiptNumber,
    string? OrderType,
    bool OriginatedFromKiosk,
    long? KioskPrepNumber,
    IReadOnlyList<PaymentDto> Payments);

public sealed record TransactionLineDto(
    Guid Id,
    Guid ItemId,
    string ItemName,
    Guid? ItemVariantId,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    IReadOnlyList<ComboSelectionDto> ComboSelections,
    IReadOnlyList<ModifierSelectionDto> ModifierSelections);

/// <summary>Mirrors Purch.Domain.Entities.TransactionLineComboSelection, resolved to
/// readable slot/item names for receipt display.</summary>
public sealed record ComboSelectionDto(
    Guid SlotId,
    string SlotLabel,
    Guid SelectedItemId,
    string SelectedItemName);

/// <summary>Mirrors Purch.Domain.Entities.TransactionLineModifierSelection, resolved
/// to readable modifier/group names and the price delta actually charged for
/// receipt display.</summary>
public sealed record ModifierSelectionDto(
    Guid ItemModifierId,
    string ModifierName,
    string ModifierGroupName,
    decimal PriceDelta);
