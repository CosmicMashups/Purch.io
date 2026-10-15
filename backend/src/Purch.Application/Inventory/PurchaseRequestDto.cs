using Purch.Domain.Enums;

namespace Purch.Application.Inventory;

public sealed record PurchaseRequestDto(
    Guid Id,
    string ControlNumber,
    Guid BranchId,
    string BranchName,
    DateOnly RequestDate,
    DateOnly? DateNeeded,
    string RequestedByName,
    string? DeliveryAddress,
    string? Remarks,
    PurchaseRequestStatus Status,
    IReadOnlyList<PurchaseRequestLineDto> Lines);

public sealed record PurchaseRequestLineDto(
    Guid Id,
    Guid ItemId,
    Guid? InventoryItemId,
    string Name,
    decimal Quantity,
    string Unit);

public sealed record CreatePurchaseRequestRequest(
    Guid BranchId,
    DateOnly? DateNeeded,
    string? Remarks,
    IReadOnlyList<CreatePurchaseRequestLineRequest> Lines);

/// <summary>Set ItemId for a catalog item, or InventoryItemId for a separately tracked ingredient.</summary>
public sealed record CreatePurchaseRequestLineRequest(
    Guid? ItemId,
    Guid? InventoryItemId,
    decimal Quantity,
    string? Unit);

/// <summary>One purchase order per entry: the request lines going to a supplier. Request lines left out are dropped.</summary>
public sealed record ConvertPurchaseRequestRequest(IReadOnlyList<ConvertPurchaseRequestOrder> Orders);

public sealed record ConvertPurchaseRequestOrder(
    Guid SupplierId,
    IReadOnlyList<Guid> LineIds,
    bool VatInclusive = false,
    decimal DiscountAmount = 0m);

public sealed record SupplierLinkDto(Guid SupplierId, string SupplierName, bool IsPreferred, decimal? UnitCost);

public sealed record SetItemSuppliersRequest(IReadOnlyList<SetItemSupplierEntry> Suppliers);

public sealed record SetItemSupplierEntry(Guid SupplierId, bool IsPreferred, decimal? UnitCost);

/// <summary>An item or ingredient that needs restocking, with what to order and who normally supplies it.</summary>
public sealed record RestockSuggestionDto(
    Guid? ItemId,
    Guid? InventoryItemId,
    string Name,
    decimal OnHand,
    decimal Threshold,
    decimal SuggestedQuantity,
    string Unit,
    decimal? UnitCost,
    IReadOnlyList<SupplierLinkDto> Suppliers,
    /// <summary>Control number of the open request or order already covering it, or null.</summary>
    string? Pending);
