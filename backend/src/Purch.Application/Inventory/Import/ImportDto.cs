using Purch.Domain.Enums;

namespace Purch.Application.Inventory.Import;

/// <summary>A saved answer to a decision, as the API sends and receives it. Choice is link, create or rename.</summary>
public sealed record ImportResolutionDto(string Choice, Guid? TargetId = null, string? NewName = null);

/// <summary>Commit takes only the batch id plus these: the plan is always rebuilt on the server. <see cref="PlanHash"/>
/// is the hash of the preview the user confirmed; <see cref="ConfirmCreations"/> confirms the Will-create group.</summary>
public sealed record CommitImportRequest(string? ApproverPin, string? PlanHash, bool ConfirmCreations, Guid? BranchId = null);

/// <summary>A batch with its plan rebuilt against the current catalog and the saved resolutions. Cost before/after
/// values in the plan are blanked for actors who cannot see costs. <see cref="RequiresBranch"/>: the plan posts stock
/// and the device has no branch, so the commit must send a BranchId.</summary>
public sealed record ImportBatchDto(
    Guid Id,
    ImportKind Kind,
    ImportBatchStatus Status,
    string ContentHash,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    ImportPlan Plan,
    string PlanHash,
    bool Blocked,
    bool RequiresBranch,
    IReadOnlyDictionary<string, ImportResolutionDto> Resolutions,
    ImportResultDto? Result);

public sealed record ImportCreatedDto(string Kind, Guid Id, string Name, IReadOnlyList<int> Lines);

public sealed record ImportMovementDto(int Line, Guid? ItemId, Guid? InventoryItemId, MovementType Type, decimal Quantity, Guid MovementId);

/// <summary>What a committed batch did; stored on the batch and returned unchanged by a repeated commit.</summary>
public sealed record ImportResultDto(
    Guid BatchId,
    IReadOnlyList<ImportCreatedDto> CreatedCategories,
    IReadOnlyList<ImportCreatedDto> CreatedRecords,
    int UpdatedCount,
    IReadOnlyList<ImportMovementDto> Movements,
    Guid ApprovedByUserId,
    DateTimeOffset CommittedAt);

/// <summary>Committed carries <see cref="Result"/>. Otherwise the preview was stale: nothing was applied, the batch is
/// still Previewed with its resolutions, and <see cref="Batch"/> holds the fresh plan with <see cref="ChangedLines"/>
/// listing the rows whose outcome differs from the preview that was confirmed.</summary>
public sealed record ImportCommitResultDto(bool Committed, ImportResultDto? Result, ImportBatchDto? Batch, IReadOnlyList<int> ChangedLines, string? Message);
