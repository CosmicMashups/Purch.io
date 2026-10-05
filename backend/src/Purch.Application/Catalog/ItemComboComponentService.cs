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

        var fixedItemIds = components.Where(component => component.ComponentItemId is not null).Select(component => component.ComponentItemId!.Value).Distinct().ToList();
        var fixedItemNames = fixedItemIds.Count == 0
            ? []
            : (await itemRepository.ListByIdsAsync(fixedItemIds, cancellationToken)).ToDictionary(item => item.Id, item => item.Name);

        return [.. components.Select(component => ToDto(component, categoryNamesById, fixedItemNames))];
    }

    public async Task<ItemComboComponentDto> CreateAsync(
        Guid parentItemId,
        CreateItemComboComponentRequest request,
        CancellationToken cancellationToken = default)
    {
        var component = new ItemComboComponent
        {
            TenantId = CurrentTenantId,
            ParentItemId = parentItemId,
        };

        await ApplyAsync(component, parentItemId, request.ComponentCategoryId, request.SlotLabel, request.Quantity, request.SubstitutionUpchargeAmount, request.ComponentItemId, request.ChoiceUpcharges, cancellationToken);

        itemComboComponentRepository.Add(component);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(component, cancellationToken);
    }

    public async Task<ItemComboComponentDto> UpdateAsync(
        Guid parentItemId,
        Guid componentId,
        UpdateItemComboComponentRequest request,
        CancellationToken cancellationToken = default)
    {
        var component = await RequireSlotAsync(parentItemId, componentId, cancellationToken);

        await ApplyAsync(component, parentItemId, request.ComponentCategoryId, request.SlotLabel, request.Quantity, request.SubstitutionUpchargeAmount, request.ComponentItemId, request.ChoiceUpcharges, cancellationToken);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(component, cancellationToken);
    }

    /// <summary>Removes the slot from the combo. Orders already placed keep their picks and show the slot as
    /// "(removed slot)" on the receipt, which TransactionService already handles.</summary>
    public async Task DeleteAsync(Guid parentItemId, Guid componentId, CancellationToken cancellationToken = default)
    {
        var component = await RequireSlotAsync(parentItemId, componentId, cancellationToken);
        itemComboComponentRepository.Remove(component);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<ItemComboComponent> RequireSlotAsync(Guid parentItemId, Guid componentId, CancellationToken cancellationToken)
    {
        _ = await itemRepository.RequirePricingTypeAsync(parentItemId, PricingType.Combo, cancellationToken);

        var component = await itemComboComponentRepository.GetTrackedAsync(componentId, cancellationToken);
        return component is null || component.ParentItemId != parentItemId
            ? throw new NotFoundException("Combo slot", componentId)
            : component;
    }

    /// <summary>Validates the slot's definition and writes it onto the entity. A slot is either a choice
    /// (pick from a category, optionally with per-choice surcharges) or fixed (exactly one named item).</summary>
    private async Task ApplyAsync(
        ItemComboComponent component,
        Guid parentItemId,
        Guid requestedCategoryId,
        string slotLabel,
        int quantity,
        decimal? flatUpcharge,
        Guid? fixedItemId,
        IReadOnlyList<ComboChoiceUpchargeDto>? choiceUpcharges,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(slotLabel))
        {
            throw new ValidationException("SlotLabel", "A slot label is required (e.g. \"Choose a Drink\").");
        }

        if (quantity < 1)
        {
            throw new ValidationException("Quantity", "Quantity must be at least 1.");
        }

        if (flatUpcharge is < 0)
        {
            throw new ValidationException("SubstitutionUpchargeAmount", "The upcharge amount cannot be negative.");
        }

        _ = await itemRepository.RequirePricingTypeAsync(parentItemId, PricingType.Combo, cancellationToken);

        var categoryId = requestedCategoryId;
        string? choiceUpchargesJson = null;

        if (fixedItemId is { } itemId)
        {
            var fixedItem = await itemRepository.GetByIdAsync(itemId, cancellationToken)
                ?? throw new NotFoundException("Item", itemId);

            if (fixedItem.Id == parentItemId || fixedItem.PricingType == PricingType.Combo)
            {
                throw new ValidationException("ComponentItemId", "A combo can't include itself or another combo. Pick one of the items it is made of.");
            }

            if (choiceUpcharges is { Count: > 0 })
            {
                throw new ValidationException("ChoiceUpcharges", "A fixed item has no choices, so it can't carry choice surcharges.");
            }

            // The slot keeps a category so everything that already reads one keeps working.
            categoryId = fixedItem.CategoryId ?? requestedCategoryId;
            if (categoryId == Guid.Empty)
            {
                throw new ValidationException("ComponentItemId", "Give this item a category first, or choose a category for the slot.");
            }

            flatUpcharge = null;
        }
        else if (choiceUpcharges is { Count: > 0 })
        {
            if (choiceUpcharges.Any(entry => entry.Amount < 0))
            {
                throw new ValidationException("ChoiceUpcharges", "A choice surcharge cannot be negative.");
            }

            if (choiceUpcharges.Select(entry => entry.ItemId).Distinct().Count() != choiceUpcharges.Count)
            {
                throw new ValidationException("ChoiceUpcharges", "Each choice can have only one surcharge.");
            }

            var items = await itemRepository.ListByIdsAsync([.. choiceUpcharges.Select(entry => entry.ItemId)], cancellationToken);
            if (items.Count != choiceUpcharges.Count || items.Any(item => item.CategoryId != categoryId))
            {
                throw new ValidationException("ChoiceUpcharges", "A choice surcharge must name an item from the slot's category.");
            }

            choiceUpchargesJson = ComboChoiceUpcharges.Serialize(choiceUpcharges);
        }

        _ = await categoryRepository.GetByIdAsync(categoryId, cancellationToken)
            ?? throw new NotFoundException("Category", categoryId);

        component.ComponentCategoryId = categoryId;
        component.ComponentItemId = fixedItemId;
        component.SlotLabel = slotLabel.Trim();
        component.Quantity = quantity;
        component.SubstitutionUpchargeAmount = flatUpcharge;
        component.ChoiceUpchargesJson = choiceUpchargesJson;
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("Combo component management requires an authenticated tenant context.");

    private async Task<ItemComboComponentDto> ToDtoAsync(ItemComboComponent component, CancellationToken cancellationToken)
    {
        var category = await categoryRepository.GetByIdAsync(component.ComponentCategoryId, cancellationToken);
        var fixedItemNames = new Dictionary<Guid, string>();
        if (component.ComponentItemId is { } fixedItemId)
        {
            var fixedItem = await itemRepository.GetByIdAsync(fixedItemId, cancellationToken);
            if (fixedItem is not null)
            {
                fixedItemNames[fixedItem.Id] = fixedItem.Name;
            }
        }

        return ToDto(component, new Dictionary<Guid, string> { [component.ComponentCategoryId] = category?.Name ?? "(unknown category)" }, fixedItemNames);
    }

    private static ItemComboComponentDto ToDto(
        ItemComboComponent component,
        Dictionary<Guid, string> categoryNamesById,
        Dictionary<Guid, string> fixedItemNames)
    {
        var categoryName = categoryNamesById.TryGetValue(component.ComponentCategoryId, out var name) ? name : "(unknown category)";
        string? fixedItemName = null;
        if (component.ComponentItemId is { } fixedItemId)
        {
            fixedItemName = fixedItemNames.TryGetValue(fixedItemId, out var itemName) ? itemName : "(deleted item)";
        }

        return new(
            component.Id,
            component.ComponentCategoryId,
            categoryName,
            component.SlotLabel,
            component.Quantity,
            component.SubstitutionUpchargeAmount,
            component.ComponentItemId,
            fixedItemName,
            ComboChoiceUpcharges.ToDtos(component.ChoiceUpchargesJson));
    }
}
