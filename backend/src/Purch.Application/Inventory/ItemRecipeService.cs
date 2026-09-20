using Purch.Application.Catalog;
using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Application.Onboarding;
using Purch.Domain.Entities;

namespace Purch.Application.Inventory;

/// <summary>The list of InventoryItem ingredients an Item consumes per order,
/// used (when the tenant has UseSeparateInventoryTracking on) both to derive
/// availability and to decrement stock on a completed sale. An item is either its own inventory
/// item (an auto-paired InventoryItem holds its stock) or made from a recipe of other inventory
/// items — never both, so setting a recipe retires the paired record and clearing it restores one.</summary>
public sealed class ItemRecipeService(
    IItemRecipeRepository recipeRepository,
    IItemRepository itemRepository,
    IInventoryItemRepository inventoryItemRepository,
    ITenantRepository tenantRepository,
    ICurrentTenantProvider currentTenantProvider,
    IUnitOfWork unitOfWork) : IItemRecipeService
{
    public async Task<IReadOnlyList<ItemRecipeLineDto>> GetRecipeAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        _ = await GetOwnedItemAsync(itemId, cancellationToken);

        var lines = await recipeRepository.ListByItemAsync(itemId, cancellationToken);
        return await ToDtosAsync(lines, cancellationToken);
    }

    public async Task<IReadOnlyList<ItemRecipeLineDto>> ReplaceRecipeAsync(Guid itemId, ReplaceItemRecipeRequest request, CancellationToken cancellationToken = default)
    {
        var item = await GetOwnedItemAsync(itemId, cancellationToken);
        var linked = await inventoryItemRepository.GetByLinkedItemIdAsync(CurrentTenantId, itemId, cancellationToken);

        foreach (var line in request.Lines)
        {
            var inventoryItem = await inventoryItemRepository.GetByIdAsync(line.InventoryItemId, cancellationToken)
                ?? throw new NotFoundException("InventoryItem", line.InventoryItemId);

            if (inventoryItem.TenantId != CurrentTenantId)
            {
                throw new NotFoundException("InventoryItem", line.InventoryItemId);
            }

            if (linked is not null && inventoryItem.Id == linked.Id)
            {
                throw new ValidationException(nameof(line.InventoryItemId), "An item can't be an ingredient of its own recipe.");
            }

            if (line.QuantityPerOrder is < 0)
            {
                throw new ValidationException(nameof(line.QuantityPerOrder), "Quantity per order cannot be negative.");
            }
        }

        if (request.Lines.Count > 0)
        {
            if (linked is not null)
            {
                // A recipe item has no stock of its own, so its paired record has to go. Refuse rather
                // than silently discard a count someone is relying on.
                if (linked.QuantityOnHand != 0)
                {
                    throw new ValidationException(
                        nameof(request.Lines),
                        "This item is tracked as its own inventory item and still has stock. An item is either an inventory item or made from a recipe, not both — bring its stock to zero first, then add a recipe.");
                }

                linked.LinkedItemId = null;
                linked.IsActive = false;
            }
        }
        else if (linked is null)
        {
            // No recipe any more, so it is a plain inventory item again.
            var tenant = await tenantRepository.GetByIdAsync(CurrentTenantId, cancellationToken);
            if (tenant is { UseSeparateInventoryTracking: true })
            {
                inventoryItemRepository.Add(new InventoryItem
                {
                    TenantId = CurrentTenantId,
                    Name = item.Name,
                    BaseUnit = "pc",
                    PackagingUnit = "pc",
                    PackagingSize = 1,
                    QuantityOnHand = 0,
                    IsAutoCreatedForItem = true,
                    LinkedItemId = item.Id,
                    IsActive = true,
                });
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

        return item.TenantId != CurrentTenantId ? throw new NotFoundException("Item", itemId) : item;
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
