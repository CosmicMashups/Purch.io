namespace Purch.Application.Inventory;

/// <remarks>IsCountedByHand: set only on the list. True when at least one recipe uses this ingredient and none of them
/// deducts it on a sale, so its stock only changes when someone counts it (like a sauce counted at the end of a shift).</remarks>
public sealed record InventoryItemDto(
    Guid Id,
    string Name,
    string? Sku,
    string BaseUnit,
    string PackagingUnit,
    decimal PackagingSize,
    decimal QuantityOnHand,
    decimal? LowStockThreshold,
    bool IsAutoCreatedForItem,
    Guid? LinkedItemId,
    bool IsActive,
    Guid? CategoryId = null,
    bool IsCountedByHand = false);

public sealed record CreateInventoryItemRequest(
    string Name,
    string? Sku,
    string BaseUnit,
    string PackagingUnit,
    decimal PackagingSize,
    decimal? LowStockThreshold,
    Guid? CategoryId = null);

public sealed record UpdateInventoryItemRequest(
    string Name,
    string? Sku,
    string BaseUnit,
    string PackagingUnit,
    decimal PackagingSize,
    decimal? LowStockThreshold,
    bool IsActive,
    Guid? CategoryId = null);

/// <summary>BranchId is required so the resulting Adjustment InventoryMovement can be attributed to a branch, matching InventoryMovement's shape.</summary>
public sealed record UpdatePhysicalCountRequest(decimal QuantityOnHand, Guid BranchId);

/// <summary>BranchId is required so the resulting StockIn InventoryMovement can be attributed to a branch, matching InventoryMovement's shape.</summary>
public sealed record ReceiveInventoryStockRequest(decimal PackagesReceived, Guid BranchId, string? SupplierReference);
