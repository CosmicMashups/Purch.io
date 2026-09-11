namespace Purch.Application.Catalog;

public interface IModifierGroupService
{
    Task<IReadOnlyList<ModifierGroupDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<ModifierGroupDto> CreateAsync(CreateModifierGroupRequest request, CancellationToken cancellationToken = default);

    Task<ModifierGroupDto> AddModifierAsync(
        Guid groupId,
        CreateItemModifierRequest request,
        CancellationToken cancellationToken = default);
}
