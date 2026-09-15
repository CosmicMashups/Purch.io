using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Domain.Entities;

namespace Purch.Application.Catalog;

public sealed class CategoryService(
    ICategoryRepository categoryRepository,
    ICurrentTenantProvider currentTenantProvider,
    IUnitOfWork unitOfWork) : ICategoryService
{
    public async Task<IReadOnlyList<CategoryDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var categories = await categoryRepository.ListByTenantAsync(CurrentTenantId, cancellationToken);
        return [.. categories.OrderBy(category => category.SortOrder).Select(ToDto)];
    }

    public async Task<CategoryDto> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException(nameof(request.Name), "Category name is required.");
        }

        var category = new Category
        {
            TenantId = CurrentTenantId,
            Name = request.Name.Trim(),
            SortOrder = request.SortOrder,
            ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim(),
        };

        categoryRepository.Add(category);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(category);
    }

    public async Task<CategoryDto> UpdateAsync(Guid categoryId, UpdateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException(nameof(request.Name), "Category name is required.");
        }

        var category = await categoryRepository.GetByIdAsync(categoryId, cancellationToken)
            ?? throw new NotFoundException("Category", categoryId);

        category.Name = request.Name.Trim();
        category.SortOrder = request.SortOrder;
        category.ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim();

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(category);
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("Category management requires an authenticated tenant context.");

    private static CategoryDto ToDto(Category category)
    {
        return new(category.Id, category.Name, category.SortOrder, category.ImageUrl);
    }
}
