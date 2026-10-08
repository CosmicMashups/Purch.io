using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Domain.Entities;

namespace Purch.Application.Catalog;

public sealed class ItemModifierGroupService(
    IItemModifierGroupRepository itemModifierGroupRepository,
    IModifierGroupRepository modifierGroupRepository,
    IItemRepository itemRepository,
    ModifierDtoBuilder dtoBuilder,
    ICurrentTenantProvider currentTenantProvider,
    IUnitOfWork unitOfWork) : IItemModifierGroupService
{
    public async Task<IReadOnlyList<ModifierGroupDto>> ListForItemAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        _ = await itemRepository.GetByIdAsync(itemId, cancellationToken)
            ?? throw new NotFoundException("Item", itemId);

        var groupIds = await itemModifierGroupRepository.ListGroupIdsForItemAsync(itemId, cancellationToken);
        var groupIdSet = groupIds.ToHashSet();

        var allGroups = await modifierGroupRepository.ListByTenantWithModifiersAsync(CurrentTenantId, ModifierListScope.Sellable, cancellationToken);
        return await dtoBuilder.BuildAsync(allGroups.Where(pair => groupIdSet.Contains(pair.Group.Id)), cancellationToken);
    }

    public async Task<ModifierGroupDto> AttachAsync(
        Guid itemId,
        AttachModifierGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = await itemRepository.GetByIdAsync(itemId, cancellationToken)
            ?? throw new NotFoundException("Item", itemId);

        var group = await modifierGroupRepository.GetByIdAsync(request.ModifierGroupId, cancellationToken)
            ?? throw new NotFoundException("Modifier group", request.ModifierGroupId);

        if (await itemModifierGroupRepository.ExistsAsync(itemId, group.Id, cancellationToken))
        {
            throw new ConflictException("This modifier group is already attached to this item.");
        }

        itemModifierGroupRepository.Add(new ItemModifierGroup
        {
            TenantId = CurrentTenantId,
            ItemId = itemId,
            ModifierGroupId = group.Id,
        });
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        var refreshed = await modifierGroupRepository.ListByTenantWithModifiersAsync(CurrentTenantId, cancellationToken);
        var pair = refreshed.First(candidate => candidate.Group.Id == group.Id);
        return (await dtoBuilder.BuildAsync([pair], cancellationToken))[0];
    }

    public async Task<AttachModifierGroupToItemsResult> AttachToItemsAsync(
        Guid groupId,
        AttachModifierGroupToItemsRequest request,
        CancellationToken cancellationToken = default)
    {
        var group = await modifierGroupRepository.GetByIdAsync(groupId, cancellationToken)
            ?? throw new NotFoundException("Modifier group", groupId);

        var chosen = request.ItemIds?.Distinct().ToList() ?? [];
        if ((request.CategoryId is null) == (chosen.Count == 0))
        {
            throw new ValidationException(nameof(request.CategoryId), "Choose a category, or choose items, but not both.");
        }

        IReadOnlyList<Item> items;
        if (request.CategoryId is { } categoryId)
        {
            items = [.. (await itemRepository.ListByTenantAsync(CurrentTenantId, cancellationToken))
                .Where(item => item.CategoryId == categoryId && item.IsActive)];
        }
        else
        {
            items = await itemRepository.ListByIdsAsync(chosen, cancellationToken);
            if (items.Count != chosen.Count)
            {
                throw new NotFoundException("Item", chosen.First(id => items.All(item => item.Id != id)));
            }
        }

        var already = (await itemModifierGroupRepository.ListItemIdsForGroupAsync(group.Id, cancellationToken)).ToHashSet();
        var attached = 0;
        foreach (var item in items.Where(item => !already.Contains(item.Id)))
        {
            itemModifierGroupRepository.Add(new ItemModifierGroup
            {
                TenantId = CurrentTenantId,
                ItemId = item.Id,
                ModifierGroupId = group.Id,
            });
            attached++;
        }

        if (attached > 0)
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new AttachModifierGroupToItemsResult(attached, items.Count - attached);
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("Modifier group management requires an authenticated tenant context.");
}
