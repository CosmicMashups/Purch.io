using System.Text.Json;
using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Application.Onboarding;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.Catalog;

public sealed class ItemService(
    IItemRepository itemRepository,
    ICategoryRepository categoryRepository,
    ITenantRepository tenantRepository,
    IDepartmentRepository departmentRepository,
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
        await ValidateDepartmentAsync(request.DepartmentId, cancellationToken);

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

        return ToDto(item);
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

        return ToDto(item);
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

        return ToDto(item);
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

    private static ItemDto ToDto(Item item)
    {
        var allowedSizes = new List<decimal>();
        if (item.TingiAllowedSizesJson is not null)
        {
            allowedSizes = JsonSerializer.Deserialize<List<decimal>>(item.TingiAllowedSizesJson) ?? [];
        }

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
        item.DepartmentId);
    }
}
