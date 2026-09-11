using Purch.Domain.Enums;

namespace Purch.Application.Inventory;

public sealed record PurchaseOrderDto(
    Guid Id,
    Guid SupplierId,
    string SupplierName,
    Guid BranchId,
    string BranchName,
    PurchaseOrderStatus Status,
    DateTimeOffset? SentAt,
    IReadOnlyList<PurchaseOrderLineDto> Lines);

public sealed record PurchaseOrderLineDto(
    Guid Id,
    Guid ItemId,
    string ItemName,
    decimal QuantityOrdered,
    decimal QuantityReceived,
    decimal ExpectedUnitCost);

public sealed record CreatePurchaseOrderRequest(
    Guid SupplierId,
    Guid BranchId,
    IReadOnlyList<CreatePurchaseOrderLineRequest> Lines);

public sealed record CreatePurchaseOrderLineRequest(
    Guid ItemId,
    decimal QuantityOrdered,
    decimal ExpectedUnitCost);

/// <summary>One PO can be received in several batches (partial deliveries) —
/// each entry adds ReceivedQuantity on top of that line's running total,
/// rather than replacing it.</summary>
public sealed record ReceivePurchaseOrderRequest(IReadOnlyList<ReceivePurchaseOrderLineRequest> Lines);

public sealed record ReceivePurchaseOrderLineRequest(Guid LineId, decimal ReceivedQuantity);
