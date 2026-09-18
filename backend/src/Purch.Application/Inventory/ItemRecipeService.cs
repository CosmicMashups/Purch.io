using Purch.Application.Catalog;
using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Domain.Entities;

namespace Purch.Application.Inventory;

/// <summary>The list of InventoryItem ingredients an Item consumes per order,
/// used (when the tenant has UseSeparateInventoryTracking on) both to derive
/// availability and to decrement stock on a completed sale.</summary>
public sealed class ItemRecipeService(
    IItemRecipeRepository recipeRepository,
    IItemRepository itemRepository,
    IInventoryItemRepository inventoryItemRepository,
    ICurrentTenantProvider currentTenantProvider,
    IUnitOfWork unitOfWork) : IItemRecipeService
{
    public async Task<IReadOnlyList<ItemRecipeLineDto>> GetRecipeAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        await GetOwnedItemAsync(itemId, cancellationToken);

        var lines = await recipeRepository.ListByItemAsync(itemId, cancellationToken);
        return await ToDtosAsync(lines, cancellationToken);
    }

    public async Task<IReadOnlyList<ItemRecipeLineDto>> ReplaceRecipeAsync(Guid itemId, ReplaceItemRecipeRequest request, CancellationToken cancellationToken = default)
    {
        await GetOwnedItemAsync(itemId, cancellationToken);

        foreach (var line in request.Lines)
        {
            var inventoryItem = await inventoryItemRepository.GetByIdAsync(line.InventoryItemId, cancellationToken)
                ?? throw new NotFoundException("InventoryItem", line.InventoryItemId);

            if (inventoryItem.TenantId != CurrentTenantId)
            {
                throw new NotFoundException("InventoryItem", line.InventoryItemId);
            }

            if (line.QuantityPerOrder is < 0)
            {
                throw new ValidationException(nameof(line.QuantityPerOrder), "Quantity per order cannot be negative.");
            }
        }

        var existing = await recipeRepository.ListByItemAsync(itemId, cancellationToken);
        recipeRepository.RemoveRange(existing);

        var newLines = request.Lines.Select(line => new ItemRecipeLine
        {
            TenantId = CurrentTenantId,
            ItemId = itemId,
            InventoryItemId = line.InventoryItemId,
            QuantityPerOrder = line.QuantityPerOrder,
        }).ToList();

        recipeRepository.AddRange(newLines);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return await ToDtosAsync(newLines, cancellationToken);
    }

    private async Task<Item> GetOwnedItemAsync(Guid itemId, CancellationToken cancellationToken)
    {
        var item = await itemRepository.GetByIdAsync(itemId, cancellationToken)
            ?? throw new NotFoundException("Item", itemId);

        if (item.TenantId != CurrentTenantId)
        {
            throw new NotFoundException("Item", itemId);
        }

        return item;
    }

    private async Task<IReadOnlyList<ItemRecipeLineDto>> ToDtosAsync(IReadOnlyCollection<ItemRecipeLine> lines, CancellationToken cancellationToken)
    {
        var inventoryItemsById = (await inventoryItemRepository.ListByIdsAsync(
                [.. lines.Select(line => line.InventoryItemId).Distinct()],
                cancellationToken))
            .ToDictionary(inventoryItem => inventoryItem.Id);

        return [.. lines.Select(line => new ItemRecipeLineDto(
            line.InventoryItemId,
            inventoryItemsById.TryGetValue(line.InventoryItemId, out var inventoryItem) ? inventoryItem.Name : "(deleted inventory item)",
            line.QuantityPerOrder))];
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("Item recipe management requires an authenticated tenant context.");
}
