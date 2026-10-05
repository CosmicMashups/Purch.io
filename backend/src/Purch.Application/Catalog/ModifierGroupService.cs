using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Application.Inventory;
using Purch.Domain.Entities;

namespace Purch.Application.Catalog;

public sealed class ModifierGroupService(
    IModifierGroupRepository modifierGroupRepository,
    ICategoryRepository categoryRepository,
    IItemRepository itemRepository,
    IItemModifierIngredientRepository ingredientRepository,
    IInventoryItemRepository inventoryItemRepository,
    ModifierDtoBuilder dtoBuilder,
    ICurrentTenantProvider currentTenantProvider,
    IUnitOfWork unitOfWork) : IModifierGroupService
{
    public async Task<IReadOnlyList<ModifierGroupDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var groups = await modifierGroupRepository.ListByTenantWithModifiersAsync(CurrentTenantId, cancellationToken);
        return await dtoBuilder.BuildAsync(groups, cancellationToken);
    }

    public async Task<ModifierGroupDto> CreateAsync(CreateModifierGroupRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException(nameof(request.Name), "Modifier group name is required.");
        }

        await ValidateCategoryAsync(request.CategoryId, cancellationToken);

        var group = new ModifierGroup
        {
            TenantId = CurrentTenantId,
            Name = request.Name.Trim(),
            AllowMultipleSelection = request.AllowMultipleSelection,
            IsRequired = request.IsRequired,
            CategoryId = request.CategoryId,
        };

        modifierGroupRepository.Add(group);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        IReadOnlyList<ItemModifier> noModifiers = [];
        return (await dtoBuilder.BuildAsync([(group, noModifiers)], cancellationToken))[0];
    }

    /// <summary>Renames a group, changes its rules, or links/unlinks its category. Changing the category drops
    /// the old category's per-item tweaks, since they belonged to items that are no longer offered.</summary>
    public async Task<ModifierGroupDto> UpdateAsync(Guid groupId, UpdateModifierGroupRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException(nameof(request.Name), "Modifier group name is required.");
        }

        var group = await modifierGroupRepository.GetByIdAsync(groupId, cancellationToken)
            ?? throw new NotFoundException("Modifier group", groupId);

        await ValidateCategoryAsync(request.CategoryId, cancellationToken);

        if (group.CategoryId != request.CategoryId)
        {
            modifierGroupRepository.RemoveCategoryItemOverrides(
                await modifierGroupRepository.ListCategoryItemOverridesForGroupAsync(groupId, cancellationToken));
        }

        group.Name = request.Name.Trim();
        group.AllowMultipleSelection = request.AllowMultipleSelection;
        group.IsRequired = request.IsRequired;
        group.CategoryId = request.CategoryId;
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return await RefreshedGroupAsync(groupId, cancellationToken);
    }

    /// <summary>Sets a group's price override / exclusion for one item of its category; the defaults (no override, not excluded) remove the row.</summary>
    public async Task<ModifierGroupDto> UpdateCategoryItemAsync(
        Guid groupId,
        Guid itemId,
        UpdateModifierCategoryItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var group = await modifierGroupRepository.GetByIdAsync(groupId, cancellationToken)
            ?? throw new NotFoundException("Modifier group", groupId);

        var item = await itemRepository.GetByIdAsync(itemId, cancellationToken)
            ?? throw new NotFoundException("Item", itemId);

        if (group.CategoryId is null || item.CategoryId != group.CategoryId)
        {
            throw new ValidationException(nameof(itemId), "This item is not in the category linked to the group.");
        }

        if (request.PriceOverride is < 0)
        {
            throw new ValidationException(nameof(request.PriceOverride), "Price cannot be negative.");
        }

        var row = await modifierGroupRepository.GetCategoryItemOverrideAsync(groupId, itemId, cancellationToken);
        if (request.PriceOverride is null && !request.IsExcluded)
        {
            if (row is not null)
            {
                modifierGroupRepository.RemoveCategoryItemOverrides([row]);
            }
        }
        else if (row is null)
        {
            modifierGroupRepository.AddCategoryItemOverride(new ModifierGroupCategoryItem
            {
                TenantId = CurrentTenantId,
                ModifierGroupId = groupId,
                ItemId = itemId,
                PriceOverride = request.PriceOverride,
                IsExcluded = request.IsExcluded,
            });
        }
        else
        {
            row.PriceOverride = request.PriceOverride;
            row.IsExcluded = request.IsExcluded;
        }

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        return await RefreshedGroupAsync(groupId, cancellationToken);
    }

    private async Task ValidateCategoryAsync(Guid? categoryId, CancellationToken cancellationToken)
    {
        if (categoryId is { } id && await categoryRepository.GetByIdAsync(id, cancellationToken) is null)
        {
            throw new NotFoundException("Category", id);
        }
    }

    public async Task<ModifierGroupDto> AddModifierAsync(
        Guid groupId,
        CreateItemModifierRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException(nameof(request.Name), "Modifier name is required.");
        }

        var group = await modifierGroupRepository.GetByIdAsync(groupId, cancellationToken)
            ?? throw new NotFoundException("Modifier group", groupId);

        var modifier = new ItemModifier
        {
            TenantId = CurrentTenantId,
            ModifierGroupId = group.Id,
            Name = request.Name.Trim(),
            PriceDelta = request.PriceDelta,
        };

        modifierGroupRepository.AddModifier(modifier);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return await RefreshedGroupAsync(groupId, cancellationToken);
    }

    public async Task<ModifierGroupDto> UpdateModifierAsync(
        Guid modifierId,
        UpdateItemModifierRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException(nameof(request.Name), "Modifier name is required.");
        }

        var modifier = await modifierGroupRepository.GetModifierByIdAsync(modifierId, cancellationToken)
            ?? throw new NotFoundException("Modifier", modifierId);

        modifier.Name = request.Name.Trim();
        modifier.PriceDelta = request.PriceDelta;
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return await RefreshedGroupAsync(modifier.ModifierGroupId, cancellationToken);
    }

    public async Task<IReadOnlyList<ModifierIngredientDto>> GetIngredientsAsync(Guid modifierId, CancellationToken cancellationToken = default)
    {
        _ = await modifierGroupRepository.GetModifierByIdAsync(modifierId, cancellationToken)
            ?? throw new NotFoundException("Modifier", modifierId);

        return await ToDtosAsync(await ingredientRepository.ListByModifierAsync(modifierId, cancellationToken), cancellationToken);
    }

    /// <summary>Replaces the whole ingredient list of a modifier, the same all-at-once way an item's recipe is set.</summary>
    public async Task<IReadOnlyList<ModifierIngredientDto>> ReplaceIngredientsAsync(
        Guid modifierId,
        ReplaceModifierIngredientsRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = await modifierGroupRepository.GetModifierByIdAsync(modifierId, cancellationToken)
            ?? throw new NotFoundException("Modifier", modifierId);

        var lines = request.Lines ?? [];
        if (lines.Select(line => line.InventoryItemId).Distinct().Count() != lines.Count)
        {
            throw new ValidationException(nameof(request.Lines), "Each ingredient can be listed only once.");
        }

        foreach (var line in lines)
        {
            var inventoryItem = await inventoryItemRepository.GetByIdAsync(line.InventoryItemId, cancellationToken);
            if (inventoryItem is null || inventoryItem.TenantId != CurrentTenantId)
            {
                throw new NotFoundException("InventoryItem", line.InventoryItemId);
            }

            if (line.QuantityPerOrder is <= 0)
            {
                throw new ValidationException(nameof(line.QuantityPerOrder), "Quantity per selection must be more than zero, or leave it empty to only check stock.");
            }
        }

        ingredientRepository.RemoveRange(await ingredientRepository.ListByModifierAsync(modifierId, cancellationToken));

        var added = lines.Select(line => new ItemModifierIngredient
        {
            TenantId = CurrentTenantId,
            ItemModifierId = modifierId,
            InventoryItemId = line.InventoryItemId,
            QuantityPerOrder = line.QuantityPerOrder,
        }).ToList();

        ingredientRepository.AddRange(added);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return await ToDtosAsync(added, cancellationToken);
    }

    private async Task<ModifierGroupDto> RefreshedGroupAsync(Guid groupId, CancellationToken cancellationToken)
    {
        var refreshed = await modifierGroupRepository.ListByTenantWithModifiersAsync(CurrentTenantId, cancellationToken);
        var pair = refreshed.First(candidate => candidate.Group.Id == groupId);
        return (await dtoBuilder.BuildAsync([pair], cancellationToken))[0];
    }

    private async Task<IReadOnlyList<ModifierIngredientDto>> ToDtosAsync(IReadOnlyCollection<ItemModifierIngredient> ingredients, CancellationToken cancellationToken)
    {
        var inventoryItemsById = (await inventoryItemRepository.ListByIdsAsync(
                [.. ingredients.Select(ingredient => ingredient.InventoryItemId).Distinct()],
                cancellationToken))
            .ToDictionary(inventoryItem => inventoryItem.Id);

        return [.. ingredients.Select(ingredient => new ModifierIngredientDto(
            ingredient.InventoryItemId,
            inventoryItemsById.TryGetValue(ingredient.InventoryItemId, out var inventoryItem) ? inventoryItem.Name : "(deleted inventory item)",
            ingredient.QuantityPerOrder))];
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("Modifier group management requires an authenticated tenant context.");
}
