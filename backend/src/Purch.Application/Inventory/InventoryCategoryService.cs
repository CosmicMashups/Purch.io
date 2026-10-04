using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Domain.Entities;

namespace Purch.Application.Inventory;

public sealed class InventoryCategoryService(
    IInventoryCategoryRepository categoryRepository,
    IInventoryItemRepository inventoryItemRepository,
    ICurrentTenantProvider currentTenantProvider,
    IUnitOfWork unitOfWork) : IInventoryCategoryService
{
    public async Task<IReadOnlyList<InventoryCategoryDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var categories = await categoryRepository.ListByTenantAsync(CurrentTenantId, cancellationToken);
        return [.. categories.OrderBy(category => category.SortOrder).ThenBy(category => category.Name).Select(ToDto)];
    }

    public async Task<InventoryCategoryDto> CreateAsync(CreateInventoryCategoryRequest request, CancellationToken cancellationToken = default)
    {
        RequireName(request.Name);

        var category = new InventoryCategory { TenantId = CurrentTenantId, Name = request.Name.Trim(), SortOrder = request.SortOrder };
        categoryRepository.Add(category);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(category);
    }

    public async Task<InventoryCategoryDto> UpdateAsync(Guid id, UpdateInventoryCategoryRequest request, CancellationToken cancellationToken = default)
    {
        RequireName(request.Name);

        var category = await GetOwnedAsync(id, cancellationToken);
        category.Name = request.Name.Trim();
        category.SortOrder = request.SortOrder;
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(category);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await GetOwnedAsync(id, cancellationToken);
        foreach (var ingredient in await inventoryItemRepository.ListByCategoryTrackedAsync(id, cancellationToken))
        {
            ingredient.CategoryId = null;
        }

        categoryRepository.Remove(category);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<InventoryCategory> GetOwnedAsync(Guid id, CancellationToken cancellationToken)
    {
        var category = await categoryRepository.GetByIdAsync(id, cancellationToken);
        return category is null || category.TenantId != CurrentTenantId ? throw new NotFoundException("InventoryCategory", id) : category;
    }

    private static void RequireName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ValidationException(nameof(name), "Category name is required.");
        }
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("Ingredient category management requires an authenticated tenant context.");

    private static InventoryCategoryDto ToDto(InventoryCategory category) => new(category.Id, category.Name, category.SortOrder);
}
