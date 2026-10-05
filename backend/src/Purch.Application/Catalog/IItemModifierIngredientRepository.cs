using Purch.Domain.Entities;

namespace Purch.Application.Catalog;

public interface IItemModifierIngredientRepository
{
    /// <summary>Tracked, so the list can be replaced in one save.</summary>
    Task<IReadOnlyList<ItemModifierIngredient>> ListByModifierAsync(Guid modifierId, CancellationToken cancellationToken = default);

    /// <summary>Read-only, the whole tenant at once, for the catalog listing.</summary>
    Task<IReadOnlyList<ItemModifierIngredient>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Read-only, for checkout and for drawing stock down on a completed sale.</summary>
    Task<IReadOnlyList<ItemModifierIngredient>> ListByModifiersAsync(IReadOnlyCollection<Guid> modifierIds, CancellationToken cancellationToken = default);

    void AddRange(IEnumerable<ItemModifierIngredient> ingredients);

    void RemoveRange(IEnumerable<ItemModifierIngredient> ingredients);
}
