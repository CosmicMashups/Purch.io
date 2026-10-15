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
    IReadOnlyList<PurchaseOrderReceiptDto> Receipts,
    string ControlNumber = "",
    Guid? PurchaseRequestId = null,
    DateOnly RequestDate = default,
    DateOnly? DateNeeded = null,
    string RequestedByName = "",
    string? DeliveryAddress = null,
    string? Remarks = null,
    string? PaymentAccountName = null,
    string? PaymentAccountNumber = null,
    bool VatInclusive = false,
    decimal Subtotal = 0m,
    decimal DiscountAmount = 0m,
    decimal VatRate = 0m,
    decimal VatAmount = 0m,
    decimal OrderTotal = 0m);

/// <summary>An Incoming Receiving Report linked to this order.</summary>
public sealed record PurchaseOrderReceiptDto(Guid ReportId, DateOnly DeliveryDate);

public sealed record PurchaseOrderLineDto(
    Guid Id,
    Guid ItemId,
    string ItemName,
    decimal QuantityOrdered,
    decimal QuantityReceived,
    decimal ExpectedUnitCost,
    Guid? InventoryItemId = null,
    string Unit = "pc",
    decimal Amount = 0m);

public sealed record CreatePurchaseOrderRequest(
    Guid SupplierId,
    Guid BranchId,
    IReadOnlyList<CreatePurchaseOrderLineRequest> Lines,
    DateOnly? DateNeeded = null,
    string? Remarks = null,
    bool VatInclusive = false,
    decimal DiscountAmount = 0m,
    Guid? PurchaseRequestId = null);

/// <summary>Replaces a Draft order's supplier, lines, VAT choice, discount and notes. The branch stays as it was.</summary>
public sealed record UpdatePurchaseOrderRequest(
    Guid SupplierId,
    IReadOnlyList<CreatePurchaseOrderLineRequest> Lines,
    DateOnly? DateNeeded = null,
    string? Remarks = null,
    bool VatInclusive = false,
    decimal DiscountAmount = 0m);

public sealed record CreatePurchaseOrderLineRequest(
    Guid ItemId,
    decimal QuantityOrdered,
    decimal ExpectedUnitCost,
    Guid? InventoryItemId = null,
    string? Unit = null);
