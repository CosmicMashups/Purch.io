using Purch.Application.Catalog;
using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.Pos;

/// <summary>
/// The cart engine — starting/mutating a device's single in-progress sale.
/// Pricing/discounts/payment/receipt-numbering are out of scope here; those
/// land with the rest of Phase 4's D5/D6 payment and receipt work.
/// </summary>
public sealed class TransactionService(
    ITransactionRepository transactionRepository,
    IItemRepository itemRepository,
    IItemVariantRepository itemVariantRepository,
    ICurrentTenantProvider currentTenantProvider,
    ICurrentActorProvider currentActorProvider,
    IUnitOfWork unitOfWork) : ITransactionService
{
    public async Task<TransactionDto> GetOrCreateOpenCartAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await GetOrCreateOpenTransactionAsync(cancellationToken);
        return await ToDtoAsync(transaction, cancellationToken);
    }

    public async Task<TransactionDto> AddLineAsync(AddTransactionLineRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Quantity <= 0)
        {
            throw new ValidationException(nameof(request.Quantity), "Quantity must be greater than zero.");
        }

        var item = await itemRepository.GetByIdAsync(request.ItemId, cancellationToken)
            ?? throw new NotFoundException("Item", request.ItemId);

        if (!item.IsActive)
        {
            throw new ValidationException(nameof(request.ItemId), "This item is not active.");
        }

        var unitPrice = item.BasePrice;

        if (request.ItemVariantId is { } variantId)
        {
            var variant = await itemVariantRepository.GetByIdAsync(variantId, cancellationToken)
                ?? throw new NotFoundException("Item variant", variantId);

            if (variant.ItemId != item.Id)
            {
                throw new ValidationException(nameof(request.ItemVariantId), "This variant does not belong to the specified item.");
            }

            unitPrice = variant.PriceOverride ?? item.BasePrice;
        }

        var cart = await GetOrCreateOpenTransactionAsync(cancellationToken);
        var lines = await transactionRepository.ListLinesAsync(cart.Id, cancellationToken);

        var existingLine = lines.FirstOrDefault(line => line.ItemId == request.ItemId && line.ItemVariantId == request.ItemVariantId);
        if (existingLine is not null)
        {
            existingLine.Quantity += request.Quantity;
            existingLine.LineTotal = existingLine.Quantity * existingLine.UnitPrice;
        }
        else
        {
            transactionRepository.AddLine(new TransactionLine
            {
                TenantId = CurrentTenantId,
                TransactionId = cart.Id,
                ItemId = request.ItemId,
                ItemVariantId = request.ItemVariantId,
                Quantity = request.Quantity,
                UnitPrice = unitPrice,
                LineTotal = unitPrice * request.Quantity,
            });
        }

        await RecalculateTotalAsync(cart, cancellationToken);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(cart, cancellationToken);
    }

    public async Task<TransactionDto> UpdateLineAsync(Guid lineId, UpdateTransactionLineRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Quantity <= 0)
        {
            throw new ValidationException(nameof(request.Quantity), "Quantity must be greater than zero.");
        }

        var line = await RequireOwnLineAsync(lineId, cancellationToken);

        line.Quantity = request.Quantity;
        line.LineTotal = line.Quantity * line.UnitPrice;

        var cart = await transactionRepository.GetByIdAsync(line.TransactionId, cancellationToken)
            ?? throw new NotFoundException("Transaction", line.TransactionId);
        await RecalculateTotalAsync(cart, cancellationToken);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(cart, cancellationToken);
    }

    public async Task<TransactionDto> RemoveLineAsync(Guid lineId, CancellationToken cancellationToken = default)
    {
        var line = await RequireOwnLineAsync(lineId, cancellationToken);
        var cart = await transactionRepository.GetByIdAsync(line.TransactionId, cancellationToken)
            ?? throw new NotFoundException("Transaction", line.TransactionId);

        transactionRepository.RemoveLine(line);

        await RecalculateTotalAsync(cart, cancellationToken, excludingLineId: line.Id);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(cart, cancellationToken);
    }

    public async Task<TransactionDto> VoidCartAsync(CancellationToken cancellationToken = default)
    {
        var deviceId = CurrentDeviceId;
        var cart = await transactionRepository.GetOpenByDeviceAsync(deviceId, cancellationToken)
            ?? throw new NotFoundException("Open cart", deviceId);

        cart.Status = TransactionStatus.Voided;
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(cart, cancellationToken);
    }

    private async Task<TransactionLine> RequireOwnLineAsync(Guid lineId, CancellationToken cancellationToken)
    {
        var line = await transactionRepository.GetLineAsync(lineId, cancellationToken)
            ?? throw new NotFoundException("Transaction line", lineId);

        var cart = await transactionRepository.GetByIdAsync(line.TransactionId, cancellationToken);
        return cart is null || cart.DeviceId != CurrentDeviceId || cart.Status != TransactionStatus.Open
            ? throw new NotFoundException("Transaction line", lineId)
            : line;
    }

    private async Task<Transaction> GetOrCreateOpenTransactionAsync(CancellationToken cancellationToken)
    {
        var deviceId = CurrentDeviceId;
        var existing = await transactionRepository.GetOpenByDeviceAsync(deviceId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var transaction = new Transaction
        {
            TenantId = CurrentTenantId,
            BranchId = CurrentBranchId,
            DeviceId = deviceId,
            StaffUserId = CurrentUserId,
            Status = TransactionStatus.Open,
        };

        transactionRepository.Add(transaction);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return transaction;
    }

    private async Task RecalculateTotalAsync(Transaction transaction, CancellationToken cancellationToken, Guid? excludingLineId = null)
    {
        var lines = await transactionRepository.ListLinesAsync(transaction.Id, cancellationToken);
        var subtotal = lines
            .Where(line => line.Id != excludingLineId)
            .Sum(line => line.LineTotal);

        transaction.TotalAmount = subtotal - transaction.DiscountAmount;
    }

    private async Task<TransactionDto> ToDtoAsync(Transaction transaction, CancellationToken cancellationToken)
    {
        var lines = await transactionRepository.ListLinesAsync(transaction.Id, cancellationToken);
        var lineDtos = new List<TransactionLineDto>();

        foreach (var line in lines)
        {
            var item = await itemRepository.GetByIdAsync(line.ItemId, cancellationToken);
            lineDtos.Add(new TransactionLineDto(
                line.Id,
                line.ItemId,
                item?.Name ?? "(deleted item)",
                line.ItemVariantId,
                line.Quantity,
                line.UnitPrice,
                line.LineTotal));
        }

        var subtotal = lineDtos.Sum(line => line.LineTotal);

        return new TransactionDto(
            transaction.Id,
            transaction.BranchId,
            transaction.DeviceId,
            transaction.Status,
            lineDtos,
            subtotal,
            transaction.DiscountAmount,
            transaction.TotalAmount);
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("The POS requires an authenticated tenant context.");

    private Guid CurrentDeviceId => currentActorProvider.DeviceId
        ?? throw new InvalidOperationException("The POS requires an authenticated device context.");

    private Guid CurrentBranchId => currentActorProvider.BranchId
        ?? throw new InvalidOperationException("The POS requires an authenticated device's branch.");

    private Guid CurrentUserId => currentActorProvider.UserId
        ?? throw new InvalidOperationException("The POS requires an authenticated staff user.");
}
