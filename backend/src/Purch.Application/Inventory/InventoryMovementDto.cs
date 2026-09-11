using Purch.Domain.Enums;

namespace Purch.Application.Inventory;

public sealed record InventoryMovementDto(
    Guid Id,
    Guid ItemId,
    string ItemName,
    Guid BranchId,
    string BranchName,
    MovementType Type,
    decimal Quantity,
    Guid StaffUserId,
    string StaffUserName,
    string? Note,
    string? ReasonCategory,
    string? PhotoUrl,
    string? SupplierReference,
    DateTimeOffset CreatedAt);

/// <summary>Quantity is always positive except for Adjustment, where a
/// negative value corrects stock downward and a positive value corrects it
/// upward — every other type's direction is implied by its Type (see
/// InventoryMovementService.StockDelta).</summary>
public sealed record RecordMovementRequest(
    Guid ItemId,
    Guid BranchId,
    MovementType Type,
    decimal Quantity,
    string? Note,
    string? ReasonCategory,
    string? PhotoUrl,
    string? SupplierReference);
