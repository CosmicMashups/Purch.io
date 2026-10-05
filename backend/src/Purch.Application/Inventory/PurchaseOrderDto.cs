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
    IReadOnlyList<PurchaseOrderLineDto> Lines,
    IReadOnlyList<PurchaseOrderReceiptDto> Receipts);

/// <summary>An Incoming Receiving Report linked to this order.</summary>
public sealed record PurchaseOrderReceiptDto(Guid ReportId, DateOnly DeliveryDate);

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
