namespace Purch.Application.Catalog;

public interface IItemModifierGroupService
{
    Task<IReadOnlyList<ModifierGroupDto>> ListForItemAsync(Guid itemId, CancellationToken cancellationToken = default);

    Task<ModifierGroupDto> AttachAsync(Guid itemId, AttachModifierGroupRequest request, CancellationToken cancellationToken = default);
}
