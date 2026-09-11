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
    decimal TotalAmount);

public sealed record TransactionLineDto(
    Guid Id,
    Guid ItemId,
    string ItemName,
    Guid? ItemVariantId,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal);
