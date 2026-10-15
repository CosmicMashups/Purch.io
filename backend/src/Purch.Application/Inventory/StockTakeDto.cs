using Purch.Domain.Enums;

namespace Purch.Application.Inventory;

/// <summary>BranchId is the branch the adjustment movements are recorded against; null means the caller's own
/// (device) branch. CategoryIds apply to ScopeKind Categories (catalog or ingredient categories), ItemIds and
/// InventoryItemIds to ScopeKind Picked.</summary>
public sealed record CreateStockTakeRequest(
    string Name,
    StockTakeScopeKind ScopeKind,
    bool Blind = false,
    Guid? BranchId = null,
    IReadOnlyList<Guid>? CategoryIds = null,
    IReadOnlyList<Guid>? ItemIds = null,
    IReadOnlyList<Guid>? InventoryItemIds = null,
    string? Note = null);

/// <summary>Counted sets a count; Skip marks the line skipped; neither (null, false) returns it to Uncounted.</summary>
public sealed record CountStockTakeLineRequest(decimal? Counted, bool Skip = false);

public sealed record ApplyStockTakeRequest(string? ApproverPin);

/// <summary>ExpectedQty and VarianceQty are null while a blind take is Counting; costs are null for roles that
/// may not see costs.</summary>
public sealed record StockTakeLineDto(
    Guid Id,
    Guid? ItemId,
    Guid? InventoryItemId,
    string Name,
    Guid? CategoryId,
    StockTakeLineState State,
    decimal? ExpectedQty,
    decimal? CountedQty,
    decimal? VarianceQty,
    decimal? UnitCostSnapshot,
    decimal? VarianceValue,
    Guid? CountedBy,
    DateTimeOffset? CountedAt,
    Guid? MovementId,
    decimal? MovementsSinceCount = null,
    bool NeedsRecount = false);

public sealed record StockTakeDto(
    Guid Id,
    string Name,
    StockTakeStatus Status,
    bool Blind,
    StockTakeScopeKind ScopeKind,
    Guid BranchId,
    Guid CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? AppliedAt,
    Guid? ApprovedByUserId,
    string? Note,
    int CountedCount,
    int UncountedCount,
    int SkippedCount,
    IReadOnlyList<StockTakeLineDto> Lines,
    IReadOnlyList<string> Warnings);

public sealed record StockTakeSummaryDto(
    Guid Id,
    string Name,
    StockTakeStatus Status,
    bool Blind,
    StockTakeScopeKind ScopeKind,
    DateTimeOffset CreatedAt,
    DateTimeOffset? AppliedAt,
    int LineCount,
    int CountedCount);
