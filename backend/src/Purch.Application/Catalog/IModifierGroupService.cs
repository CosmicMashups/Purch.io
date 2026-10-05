namespace Purch.Application.Catalog;

public interface IModifierGroupService
{
    Task<IReadOnlyList<ModifierGroupDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<ModifierGroupDto> CreateAsync(CreateModifierGroupRequest request, CancellationToken cancellationToken = default);

    Task<ModifierGroupDto> AddModifierAsync(
        Guid groupId,
        CreateItemModifierRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Renames a modifier or changes its price. Returns its group so the client can refresh it.</summary>
    Task<ModifierGroupDto> UpdateModifierAsync(
        Guid modifierId,
        UpdateItemModifierRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ModifierIngredientDto>> GetIngredientsAsync(Guid modifierId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ModifierIngredientDto>> ReplaceIngredientsAsync(
        Guid modifierId,
        ReplaceModifierIngredientsRequest request,
        CancellationToken cancellationToken = default);
}
