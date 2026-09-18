namespace Purch.Application.Inventory;

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
    bool IsActive);

public sealed record CreateInventoryItemRequest(
    string Name,
    string? Sku,
    string BaseUnit,
    string PackagingUnit,
    decimal PackagingSize,
    decimal? LowStockThreshold);

public sealed record UpdateInventoryItemRequest(
    string Name,
    string? Sku,
    string BaseUnit,
    string PackagingUnit,
    decimal PackagingSize,
    decimal? LowStockThreshold,
    bool IsActive);

/// <summary>BranchId is required so the resulting Adjustment InventoryMovement can be attributed to a branch, matching InventoryMovement's shape.</summary>
public sealed record UpdatePhysicalCountRequest(decimal QuantityOnHand, Guid BranchId);

/// <summary>BranchId is required so the resulting StockIn InventoryMovement can be attributed to a branch, matching InventoryMovement's shape.</summary>
public sealed record ReceiveInventoryStockRequest(decimal PackagesReceived, Guid BranchId, string? SupplierReference);
