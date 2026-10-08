using System.Text.Json;
using Purch.Application.Auth;
using Purch.Application.Catalog;
using Purch.Application.Common;
using Purch.Application.Reporting;
using Purch.Application.Common.Exceptions;
using Purch.Application.Onboarding;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.Inventory;

/// <summary>C2/C3 — the stock movement log and the form that records into
/// it. Every recorded movement also adjusts Item.StockOnHand in the same
/// save, so the log and the running stock count can never drift apart.</summary>
public sealed class InventoryMovementService(
    IInventoryMovementRepository movementRepository,
    IItemRepository itemRepository,
    IItemStockService itemStockService,
    IInventoryItemRepository inventoryItemRepository,
    IBranchRepository branchRepository,
    IBranchScopeGuard branchScopeGuard,
    IReportScopeResolver scopeResolver,
    IUserRepository userRepository,
    IAuditLogRepository auditLogRepository,
    ICurrentTenantProvider currentTenantProvider,
    ICurrentActorProvider currentActorProvider,
    IUnitOfWork unitOfWork) : IInventoryMovementService
{
    private static readonly HashSet<MovementType> DecreasingTypes =
    [
        MovementType.StockOut,
        MovementType.Consumption,
        MovementType.Spoiled,
        MovementType.Damaged,
        MovementType.ForReturn,
        MovementType.Transfer,
        MovementType.Sale,
    ];

    public async Task<IReadOnlyList<InventoryMovementDto>> ListAsync(
        Guid? itemId,
        Guid? branchId,
        MovementType? type,
        DateTimeOffset? before = null,
        int? limit = null,
        Guid? beforeId = null,
        Guid? inventoryItemId = null,
        CancellationToken cancellationToken = default)
    {
        // A branch- or department-scoped account only ever sees its own branch, whatever branch the query names.
        var scopedBranchId = await scopeResolver.ResolveBranchIdAsync(branchId, cancellationToken);
        var movements = await movementRepository.ListAsync(CurrentTenantId, itemId, scopedBranchId, type, before, limit, beforeId, inventoryItemId, cancellationToken);

        var dtos = new List<InventoryMovementDto>();
        foreach (var movement in movements)
        {
            dtos.Add(await ToDtoAsync(movement, cancellationToken));
        }
        return dtos;
    }

    public async Task<InventoryMovementDto> RecordAsync(RecordMovementRequest request, CancellationToken cancellationToken = default)
    {
        // Sale rows are written by a completed Cashier sale — recording one by hand would take stock
        // off a shelf with no receipt behind it. (The apps hide it from their pickers too.)
        if (request.Type == MovementType.Sale)
        {
            throw new ValidationException(nameof(request.Type), "Sale movements are recorded automatically by completed sales and can't be entered by hand.");
        }

        if (request.Quantity == 0)
        {
            throw new ValidationException(nameof(request.Quantity), "Quantity must not be zero.");
        }

        if (request.Type != MovementType.Adjustment && request.Quantity < 0)
        {
            throw new ValidationException(nameof(request.Quantity), "Quantity must be greater than zero for this movement type.");
        }

        if (request.Type == MovementType.Spoiled && string.IsNullOrWhiteSpace(request.ReasonCategory))
        {
            throw new ValidationException(nameof(request.ReasonCategory), "A reason category is required for a Spoiled movement.");
        }

        if (request.Type == MovementType.ForReturn && string.IsNullOrWhiteSpace(request.SupplierReference))
        {
            throw new ValidationException(nameof(request.SupplierReference), "A supplier reference is required for a For Return movement.");
        }

        await branchScopeGuard.EnsureAllowedAsync(request.BranchId, cancellationToken);

        if ((request.ItemId is null) == (request.InventoryItemId is null))
        {
            throw new ValidationException(nameof(request.ItemId), "Choose either an item or an ingredient.");
        }

        _ = await branchRepository.GetByIdAsync(request.BranchId, cancellationToken)
            ?? throw new NotFoundException("Branch", request.BranchId);

        Item? item = null;
        InventoryItem? ingredient = null;
        decimal stockOnHandBeforeAdjustment;
        Guid? routedTo;
        var delta = StockDelta(request.Type, request.Quantity);
        if (request.InventoryItemId is { } ingredientId)
        {
            ingredient = await inventoryItemRepository.GetByIdAsync(ingredientId, cancellationToken)
                ?? throw new NotFoundException("Ingredient", ingredientId);
            stockOnHandBeforeAdjustment = ingredient.QuantityOnHand;
            if (ingredient.QuantityOnHand + delta < 0)
            {
                throw new ValidationException(nameof(request.Quantity), $"Only {ingredient.QuantityOnHand:0.##} of {ingredient.Name} on hand.");
            }

            ingredient.QuantityOnHand += delta;
            routedTo = ingredient.Id;
        }
        else
        {
            item = await itemRepository.GetByIdAsync(request.ItemId!.Value, cancellationToken)
                ?? throw new NotFoundException("Item", request.ItemId.Value);
            // Captured before AdjustAsync mutates it in place, so the audit entry below records the actual
            // before/after transition rather than the same (already-updated) value twice.
            stockOnHandBeforeAdjustment = item.StockOnHand;
            routedTo = await itemStockService.AdjustAsync(item, delta, cancellationToken);
        }

        var movement = new InventoryMovement
        {
            TenantId = CurrentTenantId,
            ItemId = request.ItemId,
            BranchId = request.BranchId,
            Type = request.Type,
            Quantity = request.Quantity,
            StaffUserId = CurrentUserId,
            Note = request.Note,
            ReasonCategory = request.ReasonCategory,
            PhotoUrl = request.PhotoUrl,
            SupplierReference = request.SupplierReference,
            InventoryItemId = routedTo
        };

        movementRepository.Add(movement);

        // A hand-entered Adjustment is the one movement type that corrects a miscount rather than
        // recording a real-world stock event (a delivery, a sale, damage), so it gets an audit entry
        // the way a price or staff-access change does — the others already carry their own record
        // (a receipt, a supplier reference) and don't need one.
        if (request.Type == MovementType.Adjustment)
        {
            auditLogRepository.Add(new AuditLog
            {
                TenantId = CurrentTenantId,
                ActorUserId = CurrentUserId,
                ActionType = AuditActionType.InventoryAdjustment,
                TargetEntityType = ingredient is null ? nameof(Item) : nameof(InventoryItem),
                TargetEntityId = ingredient?.Id ?? item!.Id,
                BeforeStateJson = JsonSerializer.Serialize(new { stockOnHand = stockOnHandBeforeAdjustment }),
                AfterStateJson = JsonSerializer.Serialize(new { stockOnHand = ingredient?.QuantityOnHand ?? item!.StockOnHand, delta = request.Quantity, note = request.Note }),
            });
        }

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(movement, cancellationToken);
    }

    /// <summary>How much a movement changes Item.StockOnHand by. StockIn always
    /// adds; every other named type always subtracts the (always-positive)
    /// quantity; Adjustment applies the signed quantity as-is, since it's the
    /// one type meant to correct a miscount in either direction.</summary>
    private static decimal StockDelta(MovementType type, decimal quantity)
    {
        return type is MovementType.StockIn or MovementType.Adjustment
            ? quantity
            : DecreasingTypes.Contains(type) ? -quantity : 0m;
    }

    private async Task<InventoryMovementDto> ToDtoAsync(InventoryMovement movement, CancellationToken cancellationToken)
    {
        var item = movement.ItemId is { } movementItemId
            ? await itemRepository.GetByIdAsync(movementItemId, cancellationToken)
            : null;
        var inventoryItem = movement.InventoryItemId is { } inventoryItemId
            ? await inventoryItemRepository.GetByIdAsync(inventoryItemId, cancellationToken)
            : null;
        var branch = await branchRepository.GetByIdAsync(movement.BranchId, cancellationToken);
        var staffUser = await userRepository.FindActorAsync(movement.StaffUserId, cancellationToken);

        return new InventoryMovementDto(
            movement.Id,
            movement.ItemId,
            item?.Name ?? inventoryItem?.Name ?? "(deleted item)",
            movement.InventoryItemId,
            inventoryItem?.Name,
            movement.BranchId,
            branch?.Name ?? "(deleted branch)",
            movement.Type,
            movement.Quantity,
            movement.StaffUserId,
            staffUser?.Name ?? "(removed user)",
            movement.Note,
            movement.ReasonCategory,
            movement.PhotoUrl,
            movement.SupplierReference,
            movement.CreatedAt);
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("Inventory movements require an authenticated tenant context.");

    private Guid CurrentUserId => currentActorProvider.UserId
        ?? throw new InvalidOperationException("Inventory movements require an authenticated staff user.");
}
