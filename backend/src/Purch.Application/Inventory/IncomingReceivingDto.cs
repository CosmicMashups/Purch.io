using Purch.Domain.Enums;

namespace Purch.Application.Inventory;

public sealed record IncomingReceivingLineDto(
    Guid Id,
    Guid ItemId,
    string ItemName,
    decimal QuantityReceived,
    string Uom,
    decimal UnitPrice,
    ReceivingCondition Condition,
    ReceivingRemark Remark,
    Guid? InventoryItemId = null);

public sealed record IncomingReceivingDto(
    Guid Id,
    Guid? PurchaseOrderId,
    Guid SupplierId,
    string SupplierName,
    Guid BranchId,
    string BranchName,
    Guid ReceivedByUserId,
    string ReceivedByName,
    DateOnly DeliveryDate,
    string? Remarks,
    DateTimeOffset CreatedAt,
    IReadOnlyList<IncomingReceivingLineDto> Lines,
    string ControlNumber = "");

public sealed record CreateIncomingReceivingRequest(
    Guid? PurchaseOrderId,
    Guid SupplierId,
    Guid BranchId,
    DateOnly DeliveryDate,
    string? Remarks,
    IReadOnlyList<CreateIncomingReceivingLineRequest> Lines);

public sealed record CreateIncomingReceivingLineRequest(
    Guid ItemId,
    decimal QuantityReceived,
    string? Uom,
    decimal UnitPrice,
    ReceivingCondition Condition,
    ReceivingRemark Remark,
    Guid? InventoryItemId = null);

public sealed record LinkIncomingReceivingRequest(Guid PurchaseOrderId);
