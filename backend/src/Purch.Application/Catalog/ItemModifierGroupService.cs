using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Domain.Entities;

namespace Purch.Application.Catalog;

public sealed class ItemModifierGroupService(
    IItemModifierGroupRepository itemModifierGroupRepository,
    IModifierGroupRepository modifierGroupRepository,
    IItemRepository itemRepository,
    ICurrentTenantProvider currentTenantProvider,
    IUnitOfWork unitOfWork) : IItemModifierGroupService
{
    public async Task<IReadOnlyList<ModifierGroupDto>> ListForItemAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        _ = await itemRepository.GetByIdAsync(itemId, cancellationToken)
            ?? throw new NotFoundException("Item", itemId);

        var groupIds = await itemModifierGroupRepository.ListGroupIdsForItemAsync(itemId, cancellationToken);
        var groupIdSet = groupIds.ToHashSet();

        var allGroups = await modifierGroupRepository.ListByTenantWithModifiersAsync(CurrentTenantId, cancellationToken);
        return [.. allGroups
            .Where(pair => groupIdSet.Contains(pair.Group.Id))
            .Select(pair => ToDto(pair.Group, pair.Modifiers))];
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
        var (Group, Modifiers) = refreshed.First(pair => pair.Group.Id == group.Id);
        return ToDto(Group, Modifiers);
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("Modifier group management requires an authenticated tenant context.");

    private static ModifierGroupDto ToDto(ModifierGroup group, IReadOnlyList<ItemModifier> modifiers)
    {
        return new(
        group.Id,
        group.Name,
        group.AllowMultipleSelection,
        group.IsRequired,
        [.. modifiers.Select(m => new ItemModifierDto(m.Id, m.Name, m.PriceDelta))]);
    }
}
