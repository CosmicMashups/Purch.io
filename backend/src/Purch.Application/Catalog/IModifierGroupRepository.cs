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

    /// <summary>The same list, narrowed or widened by <paramref name="scope"/>.</summary>
    Task<IReadOnlyList<(ModifierGroup Group, IReadOnlyList<ItemModifier> Modifiers)>> ListByTenantWithModifiersAsync(
        Guid tenantId,
        ModifierListScope scope,
        CancellationToken cancellationToken = default);

    /// <summary>Read-only: every per-item tweak for category-linked groups in the tenant.</summary>
    Task<IReadOnlyList<ModifierGroupCategoryItem>> ListCategoryItemOverridesAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>Tracked, so the tweak can be changed in place.</summary>
    Task<ModifierGroupCategoryItem?> GetCategoryItemOverrideAsync(
        Guid groupId,
        Guid itemId,
        CancellationToken cancellationToken = default);

    /// <summary>Tracked: one group's tweaks, for removing them when its category changes.</summary>
    Task<IReadOnlyList<ModifierGroupCategoryItem>> ListCategoryItemOverridesForGroupAsync(
        Guid groupId,
        CancellationToken cancellationToken = default);

    void AddCategoryItemOverride(ModifierGroupCategoryItem row);

    void RemoveCategoryItemOverrides(IEnumerable<ModifierGroupCategoryItem> rows);

    void Add(ModifierGroup group);

    void AddModifier(ItemModifier modifier);
}

/// <summary>Which groups and modifiers a list should contain. Deleted ones are hidden from everything except History.</summary>
public enum ModifierListScope
{
    /// <summary>Back-office lists: everything not deleted, inactive included, so it can be reactivated.</summary>
    Manage,

    /// <summary>What can be sold: active groups with active modifiers only.</summary>
    Sellable,

    /// <summary>Receipts and reports: everything, deleted included, so old sales still name what was chosen.</summary>
    History,
}
