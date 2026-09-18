using System.Text.Json;
using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Application.Inventory;
using Purch.Application.Onboarding;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.Catalog;

public sealed class ItemService(
    IItemRepository itemRepository,
    ICategoryRepository categoryRepository,
    ITenantRepository tenantRepository,
    IDepartmentRepository departmentRepository,
    IItemRecipeRepository itemRecipeRepository,
    IInventoryItemRepository inventoryItemRepository,
    ICurrentTenantProvider currentTenantProvider,
    IUnitOfWork unitOfWork) : IItemService
{
    public async Task<IReadOnlyList<ItemDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var items = await itemRepository.ListByTenantAsync(CurrentTenantId, cancellationToken);
        var tenant = await GetTenantAsync(cancellationToken);

        // Batch-load once for the whole catalog instead of querying recipe
        // lines/inventory items per item (which was O(items x ingredients)
        // round trips on every Cashier/Kiosk catalog load).
        var recipeLinesByItemId = tenant.UseSeparateInventoryTracking
            ? (await itemRecipeRepository.ListByTenantAsync(CurrentTenantId, cancellationToken))
                .GroupBy(line => line.ItemId)
                .ToDictionary(group => group.Key, group => (IReadOnlyList<ItemRecipeLine>)[.. group])
            : [];

        var inventoryItems = tenant.UseSeparateInventoryTracking
            ? await inventoryItemRepository.ListByTenantAsync(CurrentTenantId, cancellationToken)
            : [];
        var inventoryItemsById = inventoryItems.ToDictionary(inventoryItem => inventoryItem.Id);
        var inventoryItemsByLinkedItemId = inventoryItems
            .Where(inventoryItem => inventoryItem.LinkedItemId is not null)
            .ToDictionary(inventoryItem => inventoryItem.LinkedItemId!.Value);

        return [.. items.Select(item => ToDto(
            item,
            tenant,
            recipeLinesByItemId.GetValueOrDefault(item.Id, []),
            inventoryItemsById,
            inventoryItemsByLinkedItemId))];
    }

    public async Task<ItemDto> CreateAsync(CreateItemRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException(nameof(request.Name), "Item name is required.");
        }

        if (request.BasePrice < 0)
        {
            throw new ValidationException(nameof(request.BasePrice), "Price cannot be negative.");
        }

        await ValidateBarcodeAsync(request.Barcode, cancellationToken);
        await ValidateCategoryAsync(request.CategoryId, cancellationToken);
        await ValidateDepartmentAsync(request.DepartmentId, cancellationToken);
        await ValidatePricingTypeAsync(request.PricingType, cancellationToken);

        var tenant = await GetTenantAsync(cancellationToken);

        var item = new Item
        {
            TenantId = CurrentTenantId,
            Name = request.Name.Trim(),
            Sku = request.Sku?.Trim(),
            Barcode = request.Barcode?.Trim(),
            CategoryId = request.CategoryId,
            BasePrice = request.BasePrice,
            ImageUrl = request.ImageUrl,
            PricingType = request.PricingType,
            DepartmentId = request.DepartmentId,
            IsActive = true,
        };

        itemRepository.Add(item);

        if (tenant.UseSeparateInventoryTracking)
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

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(item, tenant, cancellationToken);
    }

    public async Task<ItemDto> UpdateAsync(Guid itemId, UpdateItemRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException(nameof(request.Name), "Item name is required.");
        }

        if (request.BasePrice < 0)
        {
            throw new ValidationException(nameof(request.BasePrice), "Price cannot be negative.");
        }

        var item = await itemRepository.GetByIdAsync(itemId, cancellationToken)
            ?? throw new NotFoundException("Item", itemId);

        if (!string.Equals(item.Barcode, request.Barcode, StringComparison.Ordinal))
        {
            await ValidateBarcodeAsync(request.Barcode, cancellationToken);
        }

        await ValidateCategoryAsync(request.CategoryId, cancellationToken);
        await ValidateDepartmentAsync(request.DepartmentId, cancellationToken);

        item.Name = request.Name.Trim();
        item.Sku = request.Sku?.Trim();
        item.Barcode = request.Barcode?.Trim();
        item.CategoryId = request.CategoryId;
        item.BasePrice = request.BasePrice;
        item.ImageUrl = request.ImageUrl;
        item.IsActive = request.IsActive;
        item.DepartmentId = request.DepartmentId;

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(item, await GetTenantAsync(cancellationToken), cancellationToken);
    }

    public async Task<ItemDto> UpdateTingiConfigAsync(
        Guid itemId,
        UpdateTingiConfigRequest request,
        CancellationToken cancellationToken = default)
    {
        var item = await itemRepository.GetByIdAsync(itemId, cancellationToken)
            ?? throw new NotFoundException("Item", itemId);

        if (item.PricingType != PricingType.WeightVolume)
        {
            throw new ValidationException(
                nameof(item.PricingType),
                "Tingi (sub-unit) selling only applies to weight/volume-priced items.");
        }

        switch (request.TingiMode)
        {
            case TingiMode.None:
                item.TingiMode = TingiMode.None;
                item.PackagedSize = null;
                item.TingiIncrementStep = null;
                item.TingiAllowedSizesJson = null;
                break;

            case TingiMode.FixedSizes:
                if (request.PackagedSize is not > 0)
                {
                    throw new ValidationException(nameof(request.PackagedSize), "The whole pack size is required and must be greater than zero.");
                }

                if (request.AllowedSizes is null || request.AllowedSizes.Count == 0)
                {
                    throw new ValidationException(nameof(request.AllowedSizes), "At least one allowed size is required.");
                }

                if (request.AllowedSizes.Any(size => size <= 0 || size > request.PackagedSize))
                {
                    throw new ValidationException(nameof(request.AllowedSizes), "Every allowed size must be greater than zero and no larger than the pack size.");
                }

                item.TingiMode = TingiMode.FixedSizes;
                item.PackagedSize = request.PackagedSize;
                item.TingiIncrementStep = null;
                item.TingiAllowedSizesJson = JsonSerializer.Serialize(request.AllowedSizes);
                break;

            case TingiMode.Increment:
                if (request.PackagedSize is not > 0)
                {
                    throw new ValidationException(nameof(request.PackagedSize), "The whole pack size is required and must be greater than zero.");
                }

                if (request.TingiIncrementStep is not > 0 || request.TingiIncrementStep > request.PackagedSize)
                {
                    throw new ValidationException(nameof(request.TingiIncrementStep), "The increment step must be greater than zero and no larger than the pack size.");
                }

                item.TingiMode = TingiMode.Increment;
                item.PackagedSize = request.PackagedSize;
                item.TingiIncrementStep = request.TingiIncrementStep;
                item.TingiAllowedSizesJson = null;
                break;

            default:
                throw new ValidationException(nameof(request.TingiMode), "Unrecognized tingi mode.");
        }

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(item, await GetTenantAsync(cancellationToken), cancellationToken);
    }

    public async Task<ItemDto> UpdateServiceDurationAsync(
        Guid itemId,
        UpdateServiceDurationRequest request,
        CancellationToken cancellationToken = default)
    {
        var item = await itemRepository.GetByIdAsync(itemId, cancellationToken)
            ?? throw new NotFoundException("Item", itemId);

        if (item.PricingType != PricingType.Service)
        {
            throw new ValidationException(
                nameof(item.PricingType),
                "Service duration only applies to service-priced items.");
        }

        if (request.DurationMinutes <= 0)
        {
            throw new ValidationException(nameof(request.DurationMinutes), "Duration must be greater than zero minutes.");
        }

        item.ServiceDurationMinutes = request.DurationMinutes;

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(item, await GetTenantAsync(cancellationToken), cancellationToken);
    }

    private async Task ValidateBarcodeAsync(string? barcode, CancellationToken cancellationToken)
    {
        var tenant = await tenantRepository.GetByIdAsync(CurrentTenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant", CurrentTenantId);

        if (tenant.RequiresBarcodePerItem && string.IsNullOrWhiteSpace(barcode))
        {
            throw new ValidationException(
                nameof(CreateItemRequest.Barcode),
                "This business requires a barcode for every item (see business settings).");
        }

        if (!string.IsNullOrWhiteSpace(barcode) && await itemRepository.BarcodeExistsAsync(CurrentTenantId, barcode, cancellationToken))
        {
            throw new ConflictException($"An item with barcode '{barcode}' already exists.");
        }
    }

    private async Task ValidatePricingTypeAsync(PricingType pricingType, CancellationToken cancellationToken)
    {
        if (pricingType != PricingType.WeightVolume)
        {
            return;
        }

        var tenant = await tenantRepository.GetByIdAsync(CurrentTenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant", CurrentTenantId);

        if (tenant.BusinessType != BusinessType.SariSariStore)
        {
            throw new ValidationException(
                nameof(CreateItemRequest.PricingType),
                "Weight/volume (tingi) pricing is only available for sari-sari store businesses.");
        }
    }

    private async Task ValidateCategoryAsync(Guid? categoryId, CancellationToken cancellationToken)
    {
        if (categoryId is null)
        {
            return;
        }

        _ = await categoryRepository.GetByIdAsync(categoryId.Value, cancellationToken)
            ?? throw new NotFoundException("Category", categoryId.Value);
    }

    public async Task<ItemDto> UpdateDepartmentAsync(
        Guid itemId,
        UpdateItemDepartmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var item = await itemRepository.GetByIdAsync(itemId, cancellationToken)
            ?? throw new NotFoundException("Item", itemId);

        await ValidateDepartmentAsync(request.DepartmentId, cancellationToken);

        item.DepartmentId = request.DepartmentId;

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(item, await GetTenantAsync(cancellationToken), cancellationToken);
    }

    public async Task<ItemDto> UpdateLowStockThresholdAsync(
        Guid itemId,
        UpdateLowStockThresholdRequest request,
        CancellationToken cancellationToken = default)
    {
        var item = await itemRepository.GetByIdAsync(itemId, cancellationToken)
            ?? throw new NotFoundException("Item", itemId);

        if (request.Threshold is < 0)
        {
            throw new ValidationException(nameof(request.Threshold), "Threshold cannot be negative.");
        }

        item.LowStockThreshold = request.Threshold;

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(item, await GetTenantAsync(cancellationToken), cancellationToken);
    }

    private async Task ValidateDepartmentAsync(Guid? departmentId, CancellationToken cancellationToken)
    {
        if (departmentId is null)
        {
            return;
        }

        _ = await departmentRepository.GetByIdAsync(departmentId.Value, cancellationToken)
            ?? throw new NotFoundException("Department", departmentId.Value);
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("Catalog management requires an authenticated tenant context.");

    private async Task<Domain.Entities.Tenant> GetTenantAsync(CancellationToken cancellationToken)
    {
        return await tenantRepository.GetByIdAsync(CurrentTenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant", CurrentTenantId);
    }

    /// <summary>Single-item path used by Create/Update/etc. mutation endpoints, where a couple
    /// of extra queries per call is negligible. The catalog listing (ListAsync) instead batches
    /// these lookups once for the whole tenant via <see cref="ToDto"/> to avoid O(items) round trips.</summary>
    private async Task<ItemDto> ToDtoAsync(Item item, Domain.Entities.Tenant tenant, CancellationToken cancellationToken)
    {
        IReadOnlyList<ItemRecipeLine> recipeLines = [];
        var inventoryItemsById = new Dictionary<Guid, InventoryItem>();
        var inventoryItemsByLinkedItemId = new Dictionary<Guid, InventoryItem>();

        if (tenant.UseSeparateInventoryTracking)
        {
            recipeLines = await itemRecipeRepository.ListByItemAsync(item.Id, cancellationToken);
            foreach (var line in recipeLines)
            {
                var inventoryItem = await inventoryItemRepository.GetByIdAsync(line.InventoryItemId, cancellationToken);
                if (inventoryItem is not null)
                {
                    inventoryItemsById[inventoryItem.Id] = inventoryItem;
                }
            }

            if (recipeLines.Count == 0)
            {
                var linkedInventoryItem = await inventoryItemRepository.GetByLinkedItemIdAsync(item.TenantId, item.Id, cancellationToken);
                if (linkedInventoryItem is not null)
                {
                    inventoryItemsByLinkedItemId[item.Id] = linkedInventoryItem;
                }
            }
        }

        return ToDto(item, tenant, recipeLines, inventoryItemsById, inventoryItemsByLinkedItemId);
    }

    /// <summary>When UseSeparateInventoryTracking is off, availability is just StockOnHand.
    /// When it's on: an item with no recipe falls back to its auto-paired InventoryItem
    /// (LinkedItemId); an item with recipe lines is out of stock if any ingredient is
    /// depleted, or (when a per-order quantity is set) doesn't have enough left for one more order.
    /// Pure/synchronous so ListAsync can batch-load every lookup once for the whole catalog.</summary>
    private static bool ComputeIsOutOfStock(
        Item item,
        Domain.Entities.Tenant tenant,
        IReadOnlyList<ItemRecipeLine> recipeLines,
        IReadOnlyDictionary<Guid, InventoryItem> inventoryItemsById,
        IReadOnlyDictionary<Guid, InventoryItem> inventoryItemsByLinkedItemId)
    {
        if (!tenant.UseSeparateInventoryTracking)
        {
            return item.StockOnHand <= 0;
        }

        if (recipeLines.Count == 0)
        {
            return inventoryItemsByLinkedItemId.TryGetValue(item.Id, out var linkedInventoryItem)
                && linkedInventoryItem.QuantityOnHand <= 0;
        }

        foreach (var line in recipeLines)
        {
            if (!inventoryItemsById.TryGetValue(line.InventoryItemId, out var inventoryItem))
            {
                continue;
            }

            if (inventoryItem.QuantityOnHand <= 0)
            {
                return true;
            }

            if (line.QuantityPerOrder is { } quantityPerOrder && inventoryItem.QuantityOnHand < quantityPerOrder)
            {
                return true;
            }
        }

        return false;
    }

    private static ItemDto ToDto(
        Item item,
        Domain.Entities.Tenant tenant,
        IReadOnlyList<ItemRecipeLine> recipeLines,
        IReadOnlyDictionary<Guid, InventoryItem> inventoryItemsById,
        IReadOnlyDictionary<Guid, InventoryItem> inventoryItemsByLinkedItemId)
    {
        var allowedSizes = new List<decimal>();
        if (item.TingiAllowedSizesJson is not null)
        {
            allowedSizes = JsonSerializer.Deserialize<List<decimal>>(item.TingiAllowedSizesJson) ?? [];
        }

        var isOutOfStock = ComputeIsOutOfStock(item, tenant, recipeLines, inventoryItemsById, inventoryItemsByLinkedItemId);

        return new(
        item.Id,
        item.Name,
        item.Sku,
        item.Barcode,
        item.CategoryId,
        item.BasePrice,
        item.ImageUrl,
        item.PricingType,
        item.StockOnHand,
        item.IsActive,
        item.TingiMode,
        item.PackagedSize,
        item.TingiIncrementStep,
        allowedSizes,
        item.ServiceDurationMinutes,
        item.DepartmentId,
        item.LowStockThreshold,
        isOutOfStock);
    }
}
