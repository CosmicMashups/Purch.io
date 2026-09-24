using Purch.Domain.Entities;

namespace Purch.Application.Catalog;

public interface IModifierGroupRepository
{
    Task<ModifierGroup?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ItemModifier?> GetModifierByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Read-only batch lookup of modifiers with their groups — two queries for any number of ids.</summary>
    Task<IReadOnlyList<(ItemModifier Modifier, ModifierGroup? Group)>> ListModifiersWithGroupsByIdsAsync(
        IReadOnlyCollection<Guid> modifierIds,
        CancellationToken cancellationToken = default);

    /// <summary>Each group returned with its modifiers already loaded — the client always wants both together.</summary>
    Task<IReadOnlyList<(ModifierGroup Group, IReadOnlyList<ItemModifier> Modifiers)>> ListByTenantWithModifiersAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    void Add(ModifierGroup group);

    void AddModifier(ItemModifier modifier);
}
