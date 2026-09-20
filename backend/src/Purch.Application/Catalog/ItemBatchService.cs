using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Application.Inventory;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.Catalog;

public sealed class ItemBatchService(
    IItemBatchRepository itemBatchRepository,
    IItemRepository itemRepository,
    IItemStockService itemStockService,
    ICurrentTenantProvider currentTenantProvider,
    IUnitOfWork unitOfWork) : IItemBatchService
{
    public async Task<IReadOnlyList<ItemBatchDto>> ListForItemAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        _ = await itemRepository.RequirePricingTypeAsync(itemId, PricingType.WeightVolume, cancellationToken);

        var batches = await itemBatchRepository.ListByItemAsync(itemId, cancellationToken);
        return [.. batches.Select(ToDto)];
    }

    public async Task<ItemBatchDto> ReceiveAsync(Guid itemId, CreateItemBatchRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.LotNumber))
        {
            throw new ValidationException(nameof(request.LotNumber), "Lot number is required.");
        }

        if (request.QuantityReceived <= 0)
        {
            throw new ValidationException(nameof(request.QuantityReceived), "Quantity received must be greater than zero.");
        }

        var item = await itemRepository.RequirePricingTypeAsync(itemId, PricingType.WeightVolume, cancellationToken);

        var batch = new ItemBatch
        {
            TenantId = CurrentTenantId,
            ItemId = itemId,
            LotNumber = request.LotNumber.Trim(),
            ExpiryDate = request.ExpiryDate,
            QuantityReceived = request.QuantityReceived,
            QuantityRemaining = request.QuantityReceived,
        };

        itemBatchRepository.Add(batch);

        // StockOnHand is the running total across all of an item's batches — the
        // per-batch FIFO stock-out breakdown matters once POS sales exist (Phase 4).
        _ = await itemStockService.AdjustAsync(item, request.QuantityReceived, cancellationToken);

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(batch);
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("Batch management requires an authenticated tenant context.");

    private static ItemBatchDto ToDto(ItemBatch batch)
    {
        return new(
        batch.Id,
        batch.LotNumber,
        batch.ExpiryDate,
        batch.QuantityReceived,
        batch.QuantityRemaining,
        batch.ReceivedAt);
    }
}
