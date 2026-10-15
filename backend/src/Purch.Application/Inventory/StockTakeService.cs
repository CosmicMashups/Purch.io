using System.Text.Json;
using Purch.Application.Catalog;
using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Application.Onboarding;
using Purch.Application.Pos;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.Inventory;

/// <summary>
/// Stock-take sessions. Each count snapshots the on-hand quantity at the instant it is entered, and applying the take
/// posts one signed Adjustment per counted line of (counted - that snapshot). Stock is never set to the counted figure,
/// so sales and deliveries that happen after a shelf was counted survive the apply, and overlapping takes stay safe.
/// </summary>
public sealed class StockTakeService(
    IStockTakeRepository stockTakeRepository,
    IItemRepository itemRepository,
    IInventoryItemRepository inventoryItemRepository,
    IItemStockService itemStockService,
    IInventoryMovementRepository movementRepository,
    IAuditLogRepository auditLogRepository,
    IApproverAuthorizationService approverAuthorizationService,
    IBranchScopeGuard branchScopeGuard,
    ICurrentTenantProvider currentTenantProvider,
    ICurrentActorProvider currentActorProvider,
    IUnitOfWork unitOfWork) : IStockTakeService
{
    private const int MaxNameLength = 120;
    private const int MaxNoteLength = 500;

    public async Task<StockTakeDto> CreateAsync(CreateStockTakeRequest request, CancellationToken cancellationToken = default)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length == 0 || name.Length > MaxNameLength)
        {
            throw new ValidationException(nameof(request.Name), $"A name of 1 to {MaxNameLength} characters is required.");
        }

        if (request.Note is { Length: > MaxNoteLength })
        {
            throw new ValidationException(nameof(request.Note), $"The note can be at most {MaxNoteLength} characters.");
        }

        var branchId = request.BranchId ?? currentActorProvider.BranchId
            ?? throw new ValidationException(nameof(request.BranchId), "Choose the branch this stock take is for.");
        await branchScopeGuard.EnsureAllowedAsync(branchId, cancellationToken);

        var take = new StockTake
        {
            TenantId = CurrentTenantId,
            Name = name,
            Blind = request.Blind,
            ScopeKind = request.ScopeKind,
            BranchId = branchId,
            CreatedBy = CurrentUserId,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
        };
        take.Lines = await MaterializeLinesAsync(take, request, cancellationToken);
        if (take.Lines.Count == 0)
        {
            throw new ValidationException(nameof(request.ScopeKind), "Nothing with stock matches this scope.");
        }

        // Overlapping open takes are allowed (delta-based apply keeps them safe); creation only warns.
        var open = await stockTakeRepository.ListOpenLineTargetsAsync(CurrentTenantId, cancellationToken);
        var overlapping = take.Lines.Count(line => open.Contains((line.ItemId, line.InventoryItemId)));
        var warnings = overlapping > 0
            ? new[] { $"{overlapping} of these lines are also in another open stock take." }
            : [];

        stockTakeRepository.Add(take);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(take, warnings);
    }

    public async Task<StockTakeDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return ToDto(await RequireAsync(id, cancellationToken));
    }

    public async Task<IReadOnlyList<StockTakeSummaryDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var takes = await stockTakeRepository.ListAsync(CurrentTenantId, cancellationToken);
        return takes
            .OrderByDescending(take => take.CreatedAt)
            .Select(take => new StockTakeSummaryDto(
                take.Id, take.Name, take.Status, take.Blind, take.ScopeKind, take.CreatedAt, take.AppliedAt,
                take.Lines.Count, take.Lines.Count(line => line.State == StockTakeLineState.Counted)))
            .ToList();
    }

    public async Task<StockTakeDto> CountLineAsync(Guid takeId, Guid lineId, decimal? counted, bool skip, CancellationToken cancellationToken = default)
    {
        var take = await RequireAsync(takeId, cancellationToken);
        if (take.Status is not (StockTakeStatus.Counting or StockTakeStatus.Review))
        {
            throw new ConflictException("Counts can only be entered while the stock take is being counted or reviewed.");
        }

        var line = take.Lines.FirstOrDefault(l => l.Id == lineId) ?? throw new NotFoundException("Stock take line", lineId);

        if (skip || counted is null)
        {
            line.State = skip ? StockTakeLineState.Skipped : StockTakeLineState.Uncounted;
            line.CountedQty = null;
            line.ExpectedQty = null;
            line.VarianceQty = null;
            line.CountedBy = skip ? CurrentUserId : null;
            line.CountedAt = skip ? DateTimeOffset.UtcNow : null;
        }
        else
        {
            if (counted < 0)
            {
                throw new ValidationException("counted", "A count cannot be negative.");
            }

            // The expected quantity is what the system holds at the instant of counting, re-taken on every entry,
            // so the variance compares like with like even when sales continue during the take.
            var expected = await OnHandAsync(line, cancellationToken);
            line.State = StockTakeLineState.Counted;
            line.CountedQty = counted;
            line.ExpectedQty = expected;
            line.VarianceQty = counted.Value - expected;
            line.CountedBy = CurrentUserId;
            line.CountedAt = DateTimeOffset.UtcNow;
        }

        line.UpdatedAt = DateTimeOffset.UtcNow;
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(take);
    }

    public Task<StockTakeDto> StartAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return TransitionAsync(id, [StockTakeStatus.Draft], StockTakeStatus.Counting, take => take.StartedAt = DateTimeOffset.UtcNow, cancellationToken);
    }

    public Task<StockTakeDto> ToReviewAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return TransitionAsync(id, [StockTakeStatus.Counting], StockTakeStatus.Review, null, cancellationToken);
    }

    public Task<StockTakeDto> CancelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return TransitionAsync(id, [StockTakeStatus.Draft, StockTakeStatus.Counting, StockTakeStatus.Review], StockTakeStatus.Cancelled, null, cancellationToken);
    }

    public async Task<StockTakeDto> ApplyAsync(Guid id, string? approverPin, CancellationToken cancellationToken = default)
    {
        // A read-only look first, so an already-applied take returns its first result and a request that would be
        // refused anyway does not burn a PIN attempt.
        var preview = await RequireAsync(id, cancellationToken);
        if (preview.Status == StockTakeStatus.Applied)
        {
            return ToDto(preview);
        }

        EnsureApplicable(preview);

        // The PIN is checked BEFORE the transaction opens: a wrong PIN records a failed attempt that must survive the
        // error. Everything is loaded again inside the transaction, which may be retried from the start.
        var approver = await approverAuthorizationService.AuthorizeAsync(approverPin, cancellationToken);
        return await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var take = await RequireAsync(id, ct);
            if (take.Status == StockTakeStatus.Applied)
            {
                return ToDto(take);
            }

            EnsureApplicable(take);
            var posted = new List<object>();
            var missing = new List<object>();
            foreach (var line in take.Lines.Where(l => l.State == StockTakeLineState.Counted && l.VarianceQty is not null and not 0m))
            {
                var movementId = await PostAdjustmentAsync(take, line, ct);
                if (movementId is null)
                {
                    missing.Add(new { lineId = line.Id, name = line.Name });
                    continue;
                }

                line.MovementId = movementId;
                line.UpdatedAt = DateTimeOffset.UtcNow;
                posted.Add(new { lineId = line.Id, name = line.Name, variance = line.VarianceQty });
            }

            var now = DateTimeOffset.UtcNow;
            take.Status = StockTakeStatus.Applied;
            take.AppliedAt = now;
            take.ApprovedByUserId = approver.Id;
            take.UpdatedAt = now;

            auditLogRepository.Add(new AuditLog
            {
                TenantId = CurrentTenantId,
                ActorUserId = CurrentUserId,
                ActionType = AuditActionType.StockTakeApplied,
                TargetEntityType = nameof(StockTake),
                TargetEntityId = take.Id,
                ApprovedByUserId = approver.Id,
                BeforeStateJson = JsonSerializer.Serialize(new { status = nameof(StockTakeStatus.Review) }),
                AfterStateJson = JsonSerializer.Serialize(new
                {
                    stockTakeId = take.Id,
                    name = take.Name,
                    countedCount = take.Lines.Count(l => l.State == StockTakeLineState.Counted),
                    postedCount = posted.Count,
                    posted,
                    uncountedLines = take.Lines.Where(l => l.State == StockTakeLineState.Uncounted).Select(l => new { lineId = l.Id, name = l.Name }),
                    skippedLines = take.Lines.Where(l => l.State == StockTakeLineState.Skipped).Select(l => new { lineId = l.Id, name = l.Name }),
                    missingLines = missing,
                }),
            });

            _ = await unitOfWork.SaveChangesAsync(ct);
            return ToDto(take);
        }, cancellationToken);
    }

    /// <summary>Posts the line's variance as a signed Adjustment through the same stock routing as a hand-recorded
    /// movement. Returns null when the item or ingredient no longer exists.</summary>
    private async Task<Guid?> PostAdjustmentAsync(StockTake take, StockTakeLine line, CancellationToken cancellationToken)
    {
        var variance = line.VarianceQty!.Value;
        Guid? itemId = null;
        Guid? inventoryItemId;
        if (line.InventoryItemId is { } ingredientId)
        {
            var ingredient = await inventoryItemRepository.GetByIdAsync(ingredientId, cancellationToken);
            if (ingredient is null || ingredient.IsDeleted)
            {
                return null;
            }

            ingredient.QuantityOnHand += variance;
            inventoryItemId = ingredient.Id;
        }
        else
        {
            var item = await itemRepository.GetByIdAsync(line.ItemId!.Value, cancellationToken);
            if (item is null || item.IsDeleted)
            {
                return null;
            }

            itemId = item.Id;
            inventoryItemId = await itemStockService.AdjustAsync(item, variance, cancellationToken);
        }

        var movement = new InventoryMovement
        {
            TenantId = CurrentTenantId,
            ItemId = itemId,
            InventoryItemId = inventoryItemId,
            BranchId = take.BranchId,
            Type = MovementType.Adjustment,
            Quantity = variance,
            StaffUserId = CurrentUserId,
            Note = $"Stock take: {take.Name}",
        };
        movementRepository.Add(movement);
        return movement.Id;
    }

    private static void EnsureApplicable(StockTake take)
    {
        if (take.Status != StockTakeStatus.Review)
        {
            throw new ConflictException("Only a stock take in review can be applied.");
        }

        if (!take.Lines.Any(l => l.State == StockTakeLineState.Counted))
        {
            throw new ValidationException("lines", "Count at least one line before applying the stock take.");
        }
    }

    private async Task<StockTakeDto> TransitionAsync(Guid id, StockTakeStatus[] from, StockTakeStatus to, Action<StockTake>? also, CancellationToken cancellationToken)
    {
        var take = await RequireAsync(id, cancellationToken);
        if (!from.Contains(take.Status))
        {
            throw new ConflictException($"A stock take that is {take.Status} cannot move to {to}.");
        }

        take.Status = to;
        take.UpdatedAt = DateTimeOffset.UtcNow;
        also?.Invoke(take);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(take);
    }

    private async Task<decimal> OnHandAsync(StockTakeLine line, CancellationToken cancellationToken)
    {
        if (line.InventoryItemId is { } ingredientId)
        {
            var ingredient = await inventoryItemRepository.GetByIdAsync(ingredientId, cancellationToken)
                ?? throw new NotFoundException("Ingredient", ingredientId);
            return ingredient.QuantityOnHand;
        }

        var item = await itemRepository.GetByIdAsync(line.ItemId!.Value, cancellationToken)
            ?? throw new NotFoundException("Item", line.ItemId.Value);
        var onHand = await itemStockService.GetOnHandAsync([item], CurrentTenantId, cancellationToken);
        return onHand.TryGetValue(item.Id, out var quantity)
            ? quantity
            : throw new ValidationException("lineId", $"{item.Name} holds no stock of its own, so it cannot be counted.");
    }

    private async Task<List<StockTakeLine>> MaterializeLinesAsync(StockTake take, CreateStockTakeRequest request, CancellationToken cancellationToken)
    {
        // Only things that hold stock of their own: stock-holding items (GetOnHandAsync leaves out services, combos
        // and recipe items) and standalone ingredients (an item's auto-paired ingredient is counted through the item).
        var allItems = await itemRepository.ListByTenantAsync(CurrentTenantId, cancellationToken);
        var stockHolding = await itemStockService.GetOnHandAsync(allItems, CurrentTenantId, cancellationToken);
        var items = allItems.Where(item => !item.IsDeleted && stockHolding.ContainsKey(item.Id));
        var ingredients = (await inventoryItemRepository.ListByTenantAsync(CurrentTenantId, cancellationToken))
            .Where(ingredient => !ingredient.IsDeleted && ingredient.LinkedItemId is null && !ingredient.IsAutoCreatedForItem);

        switch (request.ScopeKind)
        {
            case StockTakeScopeKind.All:
                break;
            case StockTakeScopeKind.Categories:
                var categoryIds = (request.CategoryIds ?? []).ToHashSet();
                if (categoryIds.Count == 0)
                {
                    throw new ValidationException(nameof(request.CategoryIds), "Choose at least one category.");
                }

                items = items.Where(item => item.CategoryId is { } c && categoryIds.Contains(c));
                ingredients = ingredients.Where(ingredient => ingredient.CategoryId is { } c && categoryIds.Contains(c));
                break;
            case StockTakeScopeKind.Picked:
                var itemIds = (request.ItemIds ?? []).ToHashSet();
                var ingredientIds = (request.InventoryItemIds ?? []).ToHashSet();
                if (itemIds.Count + ingredientIds.Count == 0)
                {
                    throw new ValidationException(nameof(request.ItemIds), "Pick at least one item or ingredient.");
                }

                items = items.Where(item => itemIds.Contains(item.Id)).ToList();
                ingredients = ingredients.Where(ingredient => ingredientIds.Contains(ingredient.Id)).ToList();
                if (items.Count() != itemIds.Count || ingredients.Count() != ingredientIds.Count)
                {
                    throw new ValidationException(nameof(request.ItemIds), "Some picked items do not exist or hold no stock of their own.");
                }

                break;
            default:
                throw new ValidationException(nameof(request.ScopeKind), "Scope must be All, Categories or Picked.");
        }

        return items
            .Select(item => new StockTakeLine
            {
                TenantId = take.TenantId,
                StockTakeId = take.Id,
                ItemId = item.Id,
                Name = item.Name,
                CategoryId = item.CategoryId,
                UnitCostSnapshot = item.CostPrice,
            })
            .Concat(ingredients.Select(ingredient => new StockTakeLine
            {
                TenantId = take.TenantId,
                StockTakeId = take.Id,
                InventoryItemId = ingredient.Id,
                Name = ingredient.Name,
                CategoryId = ingredient.CategoryId,
                UnitCostSnapshot = ingredient.UnitCost,
            }))
            .OrderBy(line => line.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<StockTake> RequireAsync(Guid id, CancellationToken cancellationToken)
    {
        var take = await stockTakeRepository.GetAsync(id, cancellationToken);
        return take is null || take.TenantId != CurrentTenantId
            ? throw new NotFoundException("Stock take", id)
            : take;
    }

    private StockTakeDto ToDto(StockTake take, IReadOnlyList<string>? warnings = null)
    {
        var hideExpected = take.Blind && take.Status == StockTakeStatus.Counting;
        var canSeeCosts = currentActorProvider.CanSeeCosts;
        var lines = take.Lines
            .OrderBy(line => line.Name, StringComparer.OrdinalIgnoreCase)
            .Select(line => new StockTakeLineDto(
                line.Id,
                line.ItemId,
                line.InventoryItemId,
                line.Name,
                line.CategoryId,
                line.State,
                hideExpected ? null : line.ExpectedQty,
                line.CountedQty,
                hideExpected ? null : line.VarianceQty,
                canSeeCosts ? line.UnitCostSnapshot : null,
                canSeeCosts && !hideExpected && line.VarianceQty is { } v && line.UnitCostSnapshot is { } cost ? v * cost : null,
                line.CountedBy,
                line.CountedAt,
                line.MovementId))
            .ToList();

        return new StockTakeDto(
            take.Id, take.Name, take.Status, take.Blind, take.ScopeKind, take.BranchId, take.CreatedBy, take.CreatedAt,
            take.StartedAt, take.AppliedAt, take.ApprovedByUserId, take.Note,
            take.Lines.Count(l => l.State == StockTakeLineState.Counted),
            take.Lines.Count(l => l.State == StockTakeLineState.Uncounted),
            take.Lines.Count(l => l.State == StockTakeLineState.Skipped),
            lines,
            warnings ?? []);
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("Stock takes require an authenticated tenant context.");

    private Guid CurrentUserId => currentActorProvider.UserId
        ?? throw new InvalidOperationException("Stock takes require an authenticated staff user.");
}
