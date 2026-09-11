using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Domain.Entities;

namespace Purch.Application.Catalog;

public sealed class ModifierGroupService(
    IModifierGroupRepository modifierGroupRepository,
    ICurrentTenantProvider currentTenantProvider,
    IUnitOfWork unitOfWork) : IModifierGroupService
{
    public async Task<IReadOnlyList<ModifierGroupDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var groups = await modifierGroupRepository.ListByTenantWithModifiersAsync(CurrentTenantId, cancellationToken);
        return [.. groups.Select(pair => ToDto(pair.Group, pair.Modifiers))];
    }

    public async Task<ModifierGroupDto> CreateAsync(CreateModifierGroupRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException(nameof(request.Name), "Modifier group name is required.");
        }

        var group = new ModifierGroup
        {
            TenantId = CurrentTenantId,
            Name = request.Name.Trim(),
            AllowMultipleSelection = request.AllowMultipleSelection,
            IsRequired = request.IsRequired,
        };

        modifierGroupRepository.Add(group);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(group, []);
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

        var refreshed = await modifierGroupRepository.ListByTenantWithModifiersAsync(CurrentTenantId, cancellationToken);
        var (Group, Modifiers) = refreshed.First(pair => pair.Group.Id == groupId);
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
