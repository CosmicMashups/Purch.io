using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Application.Onboarding;
using Purch.Domain.Entities;

namespace Purch.Application.Catalog;

public sealed class ItemService(
    IItemRepository itemRepository,
    ICategoryRepository categoryRepository,
    ITenantRepository tenantRepository,
    ICurrentTenantProvider currentTenantProvider,
    IUnitOfWork unitOfWork) : IItemService
{
    public async Task<IReadOnlyList<ItemDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var items = await itemRepository.ListByTenantAsync(CurrentTenantId, cancellationToken);
        return [.. items.Select(ToDto)];
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
            IsActive = true,
        };

        itemRepository.Add(item);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(item);
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

        item.Name = request.Name.Trim();
        item.Sku = request.Sku?.Trim();
        item.Barcode = request.Barcode?.Trim();
        item.CategoryId = request.CategoryId;
        item.BasePrice = request.BasePrice;
        item.ImageUrl = request.ImageUrl;
        item.IsActive = request.IsActive;

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(item);
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

    private async Task ValidateCategoryAsync(Guid? categoryId, CancellationToken cancellationToken)
    {
        if (categoryId is null)
        {
            return;
        }

        _ = await categoryRepository.GetByIdAsync(categoryId.Value, cancellationToken)
            ?? throw new NotFoundException("Category", categoryId.Value);
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("Catalog management requires an authenticated tenant context.");

    private static ItemDto ToDto(Item item)
    {
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
        item.IsActive);
    }
}
