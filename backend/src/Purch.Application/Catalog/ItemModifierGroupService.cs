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

        var allGroups = await modifierGroupRepository.ListByTenantWithModifiersAsync(CurrentTenantId, cancellationToken);
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

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("Modifier group management requires an authenticated tenant context.");
}
