using Purch.Domain.Enums;

namespace Purch.Application.EquipmentInventory;

/// <remarks>UsedByItemCount: set only on the list, the number of items that need this equipment.</remarks>
public sealed record EquipmentDto(
    Guid Id,
    string Name,
    EquipmentKind Kind,
    EquipmentStatus Status,
    int? Quantity,
    string? Location,
    string? Notes,
    bool IsActive,
    int SortOrder = 0,
    int UsedByItemCount = 0);

public sealed record CreateEquipmentRequest(
    string Name,
    EquipmentKind Kind,
    int? Quantity,
    string? Location,
    string? Notes,
    EquipmentStatus Status = EquipmentStatus.Operational);

public sealed record UpdateEquipmentRequest(
    string Name,
    EquipmentKind Kind,
    int? Quantity,
    string? Location,
    string? Notes,
    bool IsActive);

public sealed record SetEquipmentStatusRequest(EquipmentStatus Status);

/// <summary>The equipment in their new order; each one's position in the list becomes its sort order.</summary>
public sealed record ReorderEquipmentRequest(IReadOnlyList<Guid> EquipmentIds);

public sealed record ItemEquipmentDto(Guid EquipmentId, string EquipmentName, EquipmentStatus Status);

public sealed record ReplaceItemEquipmentRequest(IReadOnlyList<Guid> EquipmentIds);
