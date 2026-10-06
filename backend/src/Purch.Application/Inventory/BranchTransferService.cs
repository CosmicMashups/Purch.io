using Purch.Application.Catalog;
using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Application.Onboarding;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.Inventory;

/// <summary>C4 — moving stock between two branches of the same tenant.
/// Item.StockOnHand is a single tenant-wide total (not per-branch). Stock that is on a truck is not
/// on a shelf anyone can sell from, so shipping takes it off the total and receiving puts it back
/// (net effect over a completed transfer: zero). Each leg also records a Transfer-type
/// InventoryMovement against its own branch for the per-branch audit trail. A transfer that is
/// cancelled while in transit returns the stock; shipping more than is on hand is refused.</summary>
public sealed class BranchTransferService(
    IBranchTransferRepository branchTransferRepository,
    IInventoryMovementRepository movementRepository,
    IItemRepository itemRepository,
    IInventoryItemRepository inventoryItemRepository,
    IItemStockService itemStockService,
    IBranchRepository branchRepository,
    IBranchScopeGuard branchScopeGuard,
    ICurrentTenantProvider currentTenantProvider,
    ICurrentActorProvider currentActorProvider,
    IUnitOfWork unitOfWork) : IBranchTransferService
{
    public async Task<IReadOnlyList<BranchTransferDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var transfers = await branchTransferRepository.ListByTenantAsync(CurrentTenantId, cancellationToken);

        var dtos = new List<BranchTransferDto>();
        foreach (var transfer in transfers)
        {
            dtos.Add(await ToDtoAsync(transfer, cancellationToken));
        }
        return dtos;
    }

    public async Task<BranchTransferDto> CreateAsync(CreateBranchTransferRequest request, CancellationToken cancellationToken = default)
    {
        if (request.SourceBranchId == request.DestinationBranchId)
        {
            throw new ValidationException(nameof(request.DestinationBranchId), "Source and destination branches must be different.");
        }

        if (request.Lines.Count == 0)
        {
            throw new ValidationException(nameof(request.Lines), "At least one item is required.");
        }

        // A branch account may send stock out of its own branch, not out of someone else's.
        await branchScopeGuard.EnsureAllowedAsync(request.SourceBranchId, cancellationToken);

        _ = await branchRepository.GetByIdAsync(request.SourceBranchId, cancellationToken)
            ?? throw new NotFoundException("Branch", request.SourceBranchId);
        _ = await branchRepository.GetByIdAsync(request.DestinationBranchId, cancellationToken)
            ?? throw new NotFoundException("Branch", request.DestinationBranchId);

        foreach (var line in request.Lines)
        {
            if (line.Quantity <= 0)
            {
                throw new ValidationException(nameof(line.Quantity), "Quantity must be greater than zero.");
            }

            if ((line.ItemId is null) == (line.InventoryItemId is null))
            {
                throw new ValidationException(nameof(line.ItemId), "Each line needs either an item or an ingredient.");
            }

            if (line.ItemId is { } lineItemId)
            {
                _ = await itemRepository.GetByIdAsync(lineItemId, cancellationToken)
                    ?? throw new NotFoundException("Item", lineItemId);
            }
            else
            {
                _ = await inventoryItemRepository.GetByIdAsync(line.InventoryItemId!.Value, cancellationToken)
                    ?? throw new NotFoundException("Ingredient", line.InventoryItemId.Value);
            }
        }

        var transfer = new BranchTransfer
        {
            TenantId = CurrentTenantId,
            SourceBranchId = request.SourceBranchId,
            DestinationBranchId = request.DestinationBranchId,
            Status = BranchTransferStatus.Pending,
        };
        branchTransferRepository.Add(transfer);

        foreach (var line in request.Lines)
        {
            branchTransferRepository.AddLine(new BranchTransferLine
            {
                TenantId = CurrentTenantId,
                BranchTransferId = transfer.Id,
                ItemId = line.ItemId,
                InventoryItemId = line.InventoryItemId,
                Quantity = line.Quantity,
            });
        }

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(transfer, cancellationToken);
    }

    public async Task<BranchTransferDto> MarkInTransitAsync(Guid branchTransferId, CancellationToken cancellationToken = default)
    {
        var transfer = await branchTransferRepository.GetByIdAsync(branchTransferId, cancellationToken)
            ?? throw new NotFoundException("Branch transfer", branchTransferId);

        await branchScopeGuard.EnsureAllowedAsync(transfer.SourceBranchId, cancellationToken);

        if (transfer.Status != BranchTransferStatus.Pending)
        {
            throw new ValidationException(nameof(transfer.Status), "Only a Pending transfer can be marked In Transit.");
        }

        var lines = await branchTransferRepository.ListLinesAsync(transfer.Id, cancellationToken);
        foreach (var line in lines)
        {
            // Can't ship what isn't there: the total would go negative and the count stop meaning anything.
            var shippedFrom = await AdjustLineAsync(line, -line.Quantity, checkOnHand: true, cancellationToken);

            movementRepository.Add(new InventoryMovement
            {
                TenantId = CurrentTenantId,
                ItemId = line.ItemId,
                InventoryItemId = shippedFrom,
                BranchId = transfer.SourceBranchId,
                Type = MovementType.Transfer,
                Quantity = line.Quantity,
                StaffUserId = CurrentUserId,
                Note = $"Shipped on transfer {transfer.Id}",
            });
        }

        transfer.Status = BranchTransferStatus.InTransit;
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(transfer, cancellationToken);
    }

    public async Task<BranchTransferDto> MarkReceivedAsync(Guid branchTransferId, CancellationToken cancellationToken = default)
    {
        var transfer = await branchTransferRepository.GetByIdAsync(branchTransferId, cancellationToken)
            ?? throw new NotFoundException("Branch transfer", branchTransferId);

        await branchScopeGuard.EnsureAllowedAsync(transfer.DestinationBranchId, cancellationToken);

        if (transfer.Status != BranchTransferStatus.InTransit)
        {
            throw new ValidationException(nameof(transfer.Status), "Only an In Transit transfer can be marked Received.");
        }

        var lines = await branchTransferRepository.ListLinesAsync(transfer.Id, cancellationToken);
        foreach (var line in lines)
        {
            var receivedInto = await AdjustLineAsync(line, line.Quantity, checkOnHand: false, cancellationToken);

            movementRepository.Add(new InventoryMovement
            {
                TenantId = CurrentTenantId,
                ItemId = line.ItemId,
                InventoryItemId = receivedInto,
                BranchId = transfer.DestinationBranchId,
                Type = MovementType.Transfer,
                Quantity = line.Quantity,
                StaffUserId = CurrentUserId,
                Note = $"Received on transfer {transfer.Id}",
            });
        }

        transfer.Status = BranchTransferStatus.Received;
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(transfer, cancellationToken);
    }

    public async Task<BranchTransferDto> CancelAsync(Guid branchTransferId, CancellationToken cancellationToken = default)
    {
        var transfer = await branchTransferRepository.GetByIdAsync(branchTransferId, cancellationToken)
            ?? throw new NotFoundException("Branch transfer", branchTransferId);

        await branchScopeGuard.EnsureAnyAllowedAsync([transfer.SourceBranchId, transfer.DestinationBranchId], cancellationToken);

        if (transfer.Status is not (BranchTransferStatus.Pending or BranchTransferStatus.InTransit))
        {
            throw new ValidationException(nameof(transfer.Status), "Only a Pending or In Transit transfer can be cancelled.");
        }

        if (transfer.Status == BranchTransferStatus.InTransit)
        {
            // It already left the source's count when it shipped; put it back.
            foreach (var line in await branchTransferRepository.ListLinesAsync(transfer.Id, cancellationToken))
            {
                var returnedTo = await AdjustLineAsync(line, line.Quantity, checkOnHand: false, cancellationToken);

                movementRepository.Add(new InventoryMovement
                {
                    TenantId = CurrentTenantId,
                    ItemId = line.ItemId,
                    InventoryItemId = returnedTo,
                    BranchId = transfer.SourceBranchId,
                    Type = MovementType.Transfer,
                    Quantity = line.Quantity,
                    StaffUserId = CurrentUserId,
                    Note = $"Returned to source: transfer {transfer.Id} cancelled",
                });
            }
        }

        transfer.Status = BranchTransferStatus.Cancelled;
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(transfer, cancellationToken);
    }

    /// <summary>Applies a transfer leg to the line's stock count (an item's, or a standalone ingredient's) and returns the
    /// InventoryItem id to record on the movement, or null when Item.StockOnHand was changed.</summary>
    private async Task<Guid?> AdjustLineAsync(BranchTransferLine line, decimal delta, bool checkOnHand, CancellationToken cancellationToken)
    {
        if (line.InventoryItemId is { } ingredientId)
        {
            var ingredient = await inventoryItemRepository.GetByIdAsync(ingredientId, cancellationToken)
                ?? throw new NotFoundException("Ingredient", ingredientId);
            if (checkOnHand && ingredient.QuantityOnHand + delta < 0)
            {
                throw new ValidationException(
                    nameof(line.Quantity),
                    $"Only {ingredient.QuantityOnHand:0.##} of {ingredient.Name} on hand; can't ship {line.Quantity:0.##}.");
            }

            ingredient.QuantityOnHand += delta;
            return ingredient.Id;
        }

        var itemId = line.ItemId ?? throw new InvalidOperationException("A transfer line has neither an item nor an ingredient.");
        var item = await itemRepository.GetByIdAsync(itemId, cancellationToken)
            ?? throw new NotFoundException("Item", itemId);
        if (checkOnHand)
        {
            var onHand = await itemStockService.GetOnHandAsync([item], CurrentTenantId, cancellationToken);
            if (onHand.TryGetValue(item.Id, out var available) && available < line.Quantity)
            {
                throw new ValidationException(
                    nameof(line.Quantity),
                    $"Only {available:0.##} of {item.Name} on hand; can't ship {line.Quantity:0.##}.");
            }
        }

        return await itemStockService.AdjustAsync(item, delta, cancellationToken);
    }

    private async Task<BranchTransferDto> ToDtoAsync(BranchTransfer transfer, CancellationToken cancellationToken)
    {
        var sourceBranch = await branchRepository.GetByIdAsync(transfer.SourceBranchId, cancellationToken);
        var destinationBranch = await branchRepository.GetByIdAsync(transfer.DestinationBranchId, cancellationToken);
        var lines = await branchTransferRepository.ListLinesAsync(transfer.Id, cancellationToken);

        var lineDtos = new List<BranchTransferLineDto>();
        foreach (var line in lines)
        {
            string? name = null;
            if (line.ItemId is { } lineItemId)
            {
                name = (await itemRepository.GetByIdAsync(lineItemId, cancellationToken))?.Name;
            }
            else if (line.InventoryItemId is { } ingredientId)
            {
                name = (await inventoryItemRepository.GetByIdAsync(ingredientId, cancellationToken))?.Name;
            }

            lineDtos.Add(new BranchTransferLineDto(line.Id, line.ItemId, line.InventoryItemId, name ?? "(deleted item)", line.Quantity));
        }

        return new BranchTransferDto(
            transfer.Id,
            transfer.SourceBranchId,
            sourceBranch?.Name ?? "(deleted branch)",
            transfer.DestinationBranchId,
            destinationBranch?.Name ?? "(deleted branch)",
            transfer.Status,
            lineDtos);
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("Branch transfers require an authenticated tenant context.");

    private Guid CurrentUserId => currentActorProvider.UserId
        ?? throw new InvalidOperationException("Branch transfers require an authenticated staff user.");
}
