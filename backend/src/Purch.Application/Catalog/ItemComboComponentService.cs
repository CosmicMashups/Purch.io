using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.Catalog;

public sealed class ItemComboComponentService(
    IItemComboComponentRepository itemComboComponentRepository,
    IItemRepository itemRepository,
    ICategoryRepository categoryRepository,
    ICurrentTenantProvider currentTenantProvider,
    IUnitOfWork unitOfWork) : IItemComboComponentService
{
    public async Task<IReadOnlyList<ItemComboComponentDto>> ListForItemAsync(
        Guid parentItemId,
        CancellationToken cancellationToken = default)
    {
        _ = await itemRepository.RequirePricingTypeAsync(parentItemId, PricingType.Combo, cancellationToken);

        var components = await itemComboComponentRepository.ListByItemAsync(parentItemId, cancellationToken);
        var categories = await categoryRepository.ListByTenantAsync(CurrentTenantId, cancellationToken);
        var categoryNamesById = categories.ToDictionary(category => category.Id, category => category.Name);

        return [.. components.Select(component => ToDto(component, categoryNamesById))];
    }

    public async Task<ItemComboComponentDto> CreateAsync(
        Guid parentItemId,
        CreateItemComboComponentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.SlotLabel))
        {
            throw new ValidationException(nameof(request.SlotLabel), "A slot label is required (e.g. \"Choose a Drink\").");
        }

        if (request.Quantity < 1)
        {
            throw new ValidationException(nameof(request.Quantity), "Quantity must be at least 1.");
        }

        if (request.SubstitutionUpchargeAmount is < 0)
        {
            throw new ValidationException(nameof(request.SubstitutionUpchargeAmount), "The upcharge amount cannot be negative.");
        }

        _ = await itemRepository.RequirePricingTypeAsync(parentItemId, PricingType.Combo, cancellationToken);

        var category = await categoryRepository.GetByIdAsync(request.ComponentCategoryId, cancellationToken)
            ?? throw new NotFoundException("Category", request.ComponentCategoryId);

        var component = new ItemComboComponent
        {
            TenantId = CurrentTenantId,
            ParentItemId = parentItemId,
            ComponentCategoryId = request.ComponentCategoryId,
            SlotLabel = request.SlotLabel.Trim(),
            Quantity = request.Quantity,
            SubstitutionUpchargeAmount = request.SubstitutionUpchargeAmount,
        };

        itemComboComponentRepository.Add(component);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(component, category.Name);
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("Combo component management requires an authenticated tenant context.");

    private static ItemComboComponentDto ToDto(ItemComboComponent component, Dictionary<Guid, string> categoryNamesById)
    {
        var categoryName = categoryNamesById.TryGetValue(component.ComponentCategoryId, out var name) ? name : "(unknown category)";
        return ToDto(component, categoryName);
    }

    private static ItemComboComponentDto ToDto(ItemComboComponent component, string categoryName)
    {
        return new(
            component.Id,
            component.ComponentCategoryId,
            categoryName,
            component.SlotLabel,
            component.Quantity,
            component.SubstitutionUpchargeAmount);
    }
}
