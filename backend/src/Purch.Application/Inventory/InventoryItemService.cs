using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.Inventory;

/// <summary>CRUD for separately tracked InventoryItem stock records, plus the
/// two everyday stock-count operations (a manual recount, and receiving a
/// delivery). Both operations log an InventoryMovement against InventoryItemId
/// (ItemId left null) in the same save as the quantity change, mirroring how
/// InventoryMovementService keeps Item.StockOnHand and its movement log from
/// drifting apart.</summary>
public sealed class InventoryItemService(
    IInventoryItemRepository inventoryItemRepository,
    IInventoryMovementRepository movementRepository,
    IInventoryCategoryRepository categoryRepository,
    IItemRecipeRepository recipeRepository,
    IBranchScopeGuard branchScopeGuard,
    ICurrentTenantProvider currentTenantProvider,
    ICurrentActorProvider currentActorProvider,
    IUnitOfWork unitOfWork) : IInventoryItemService
{
    public async Task<IReadOnlyList<InventoryItemDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var items = await inventoryItemRepository.ListByTenantAsync(CurrentTenantId, cancellationToken);

        var recipeLines = await recipeRepository.ListByTenantAsync(CurrentTenantId, cancellationToken);
        var usedInARecipe = recipeLines.Select(line => line.InventoryItemId).ToHashSet();
        var deductedBySomeRecipe = recipeLines.Where(line => line.QuantityPerOrder is not null).Select(line => line.InventoryItemId).ToHashSet();

        return [.. items.OrderBy(item => item.Name).Select(item => ToDto(item) with
        {
            IsCountedByHand = usedInARecipe.Contains(item.Id) && !deductedBySomeRecipe.Contains(item.Id),
        })];
    }

    public async Task<InventoryItemDto> CreateAsync(CreateInventoryItemRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException(nameof(request.Name), "Inventory item name is required.");
        }

        if (request.PackagingSize <= 0)
        {
            throw new ValidationException(nameof(request.PackagingSize), "Packaging size must be greater than zero.");
        }

        var inventoryItem = new InventoryItem
        {
            TenantId = CurrentTenantId,
            Name = request.Name.Trim(),
            Sku = request.Sku?.Trim(),
            BaseUnit = string.IsNullOrWhiteSpace(request.BaseUnit) ? "pc" : request.BaseUnit.Trim(),
            PackagingUnit = string.IsNullOrWhiteSpace(request.PackagingUnit) ? "pc" : request.PackagingUnit.Trim(),
            PackagingSize = request.PackagingSize,
            LowStockThreshold = request.LowStockThreshold,
            CategoryId = await ValidCategoryAsync(request.CategoryId, cancellationToken),
            IsActive = true,
        };

        inventoryItemRepository.Add(inventoryItem);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(inventoryItem);
    }

    public async Task<InventoryItemDto> UpdateAsync(Guid inventoryItemId, UpdateInventoryItemRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException(nameof(request.Name), "Inventory item name is required.");
        }

        if (request.PackagingSize <= 0)
        {
            throw new ValidationException(nameof(request.PackagingSize), "Packaging size must be greater than zero.");
        }

        var inventoryItem = await GetOwnedAsync(inventoryItemId, cancellationToken);

        inventoryItem.Name = request.Name.Trim();
        inventoryItem.Sku = request.Sku?.Trim();
        inventoryItem.BaseUnit = string.IsNullOrWhiteSpace(request.BaseUnit) ? "pc" : request.BaseUnit.Trim();
        inventoryItem.PackagingUnit = string.IsNullOrWhiteSpace(request.PackagingUnit) ? "pc" : request.PackagingUnit.Trim();
        inventoryItem.PackagingSize = request.PackagingSize;
        inventoryItem.LowStockThreshold = request.LowStockThreshold;
        inventoryItem.CategoryId = await ValidCategoryAsync(request.CategoryId, cancellationToken);
        inventoryItem.IsActive = request.IsActive;

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(inventoryItem);
    }

    public async Task<InventoryItemDto> UpdatePhysicalCountAsync(Guid inventoryItemId, UpdatePhysicalCountRequest request, CancellationToken cancellationToken = default)
    {
        await branchScopeGuard.EnsureAllowedAsync(request.BranchId, cancellationToken);

        if (request.QuantityOnHand < 0)
        {
            throw new ValidationException(nameof(request.QuantityOnHand), "Quantity on hand cannot be negative.");
        }

        var inventoryItem = await GetOwnedAsync(inventoryItemId, cancellationToken);
        var delta = request.QuantityOnHand - inventoryItem.QuantityOnHand;
        inventoryItem.QuantityOnHand = request.QuantityOnHand;

        movementRepository.Add(new InventoryMovement
        {
            TenantId = CurrentTenantId,
            InventoryItemId = inventoryItem.Id,
            BranchId = request.BranchId,
            Type = MovementType.Adjustment,
            Quantity = delta,
            StaffUserId = CurrentUserId,
            Note = "Physical count",
        });

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(inventoryItem);
    }

    public async Task<InventoryItemDto> ReceiveStockAsync(Guid inventoryItemId, ReceiveInventoryStockRequest request, CancellationToken cancellationToken = default)
    {
        await branchScopeGuard.EnsureAllowedAsync(request.BranchId, cancellationToken);

        if (request.PackagesReceived <= 0)
        {
            throw new ValidationException(nameof(request.PackagesReceived), "Packages received must be greater than zero.");
        }

        var inventoryItem = await GetOwnedAsync(inventoryItemId, cancellationToken);
        var receivedQuantity = request.PackagesReceived * inventoryItem.PackagingSize;
        inventoryItem.QuantityOnHand += receivedQuantity;

        movementRepository.Add(new InventoryMovement
        {
            TenantId = CurrentTenantId,
            InventoryItemId = inventoryItem.Id,
            BranchId = request.BranchId,
            Type = MovementType.StockIn,
            Quantity = receivedQuantity,
            StaffUserId = CurrentUserId,
            SupplierReference = request.SupplierReference,
        });

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(inventoryItem);
    }

    private async Task<Guid?> ValidCategoryAsync(Guid? categoryId, CancellationToken cancellationToken)
    {
        if (categoryId is not { } id)
        {
            return null;
        }

        var category = await categoryRepository.GetByIdAsync(id, cancellationToken);
        return category is null || category.TenantId != CurrentTenantId
            ? throw new ValidationException(nameof(categoryId), "That ingredient category does not exist.")
            : id;
    }

    private async Task<InventoryItem> GetOwnedAsync(Guid inventoryItemId, CancellationToken cancellationToken)
    {
        var inventoryItem = await inventoryItemRepository.GetByIdAsync(inventoryItemId, cancellationToken)
            ?? throw new NotFoundException("InventoryItem", inventoryItemId);

        return inventoryItem.TenantId != CurrentTenantId ? throw new NotFoundException("InventoryItem", inventoryItemId) : inventoryItem;
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("Inventory item management requires an authenticated tenant context.");

    private Guid CurrentUserId => currentActorProvider.UserId
        ?? throw new InvalidOperationException("Inventory item management requires an authenticated staff user.");

    private static InventoryItemDto ToDto(InventoryItem inventoryItem)
    {
        return new(
            inventoryItem.Id,
            inventoryItem.Name,
            inventoryItem.Sku,
            inventoryItem.BaseUnit,
            inventoryItem.PackagingUnit,
            inventoryItem.PackagingSize,
            inventoryItem.QuantityOnHand,
            inventoryItem.LowStockThreshold,
            inventoryItem.IsAutoCreatedForItem,
            inventoryItem.LinkedItemId,
            inventoryItem.IsActive,
            inventoryItem.CategoryId);
    }
}
