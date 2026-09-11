using Purch.Application.Catalog;
using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Application.Onboarding;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.Inventory;

/// <summary>C4 — moving stock between two branches of the same tenant.
/// Item.StockOnHand is a single tenant-wide total (not per-branch), so a
/// transfer's net effect on it is zero — a Transfer-type InventoryMovement
/// is recorded at each leg (a decrease at the source branch when it ships,
/// an increase at the destination branch when it's received) purely for the
/// per-branch audit trail in the movement log, the same MovementType.Transfer
/// C2/C3 already uses for a manually-recorded transfer-out entry.</summary>
public sealed class BranchTransferService(
    IBranchTransferRepository branchTransferRepository,
    IInventoryMovementRepository movementRepository,
    IItemRepository itemRepository,
    IBranchRepository branchRepository,
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

            _ = await itemRepository.GetByIdAsync(line.ItemId, cancellationToken)
                ?? throw new NotFoundException("Item", line.ItemId);
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

        if (transfer.Status != BranchTransferStatus.Pending)
        {
            throw new ValidationException(nameof(transfer.Status), "Only a Pending transfer can be marked In Transit.");
        }

        var lines = await branchTransferRepository.ListLinesAsync(transfer.Id, cancellationToken);
        foreach (var line in lines)
        {
            var item = await itemRepository.GetByIdAsync(line.ItemId, cancellationToken)
                ?? throw new NotFoundException("Item", line.ItemId);

            item.StockOnHand -= line.Quantity;

            movementRepository.Add(new InventoryMovement
            {
                TenantId = CurrentTenantId,
                ItemId = line.ItemId,
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

        if (transfer.Status != BranchTransferStatus.InTransit)
        {
            throw new ValidationException(nameof(transfer.Status), "Only an In Transit transfer can be marked Received.");
        }

        var lines = await branchTransferRepository.ListLinesAsync(transfer.Id, cancellationToken);
        foreach (var line in lines)
        {
            var item = await itemRepository.GetByIdAsync(line.ItemId, cancellationToken)
                ?? throw new NotFoundException("Item", line.ItemId);

            item.StockOnHand += line.Quantity;

            movementRepository.Add(new InventoryMovement
            {
                TenantId = CurrentTenantId,
                ItemId = line.ItemId,
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

    private async Task<BranchTransferDto> ToDtoAsync(BranchTransfer transfer, CancellationToken cancellationToken)
    {
        var sourceBranch = await branchRepository.GetByIdAsync(transfer.SourceBranchId, cancellationToken);
        var destinationBranch = await branchRepository.GetByIdAsync(transfer.DestinationBranchId, cancellationToken);
        var lines = await branchTransferRepository.ListLinesAsync(transfer.Id, cancellationToken);

        var lineDtos = new List<BranchTransferLineDto>();
        foreach (var line in lines)
        {
            var item = await itemRepository.GetByIdAsync(line.ItemId, cancellationToken);
            lineDtos.Add(new BranchTransferLineDto(line.Id, line.ItemId, item?.Name ?? "(deleted item)", line.Quantity));
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
