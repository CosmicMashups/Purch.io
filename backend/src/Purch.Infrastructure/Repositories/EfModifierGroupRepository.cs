using Microsoft.EntityFrameworkCore;
using Purch.Application.Catalog;
using Purch.Domain.Entities;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfModifierGroupRepository(PurchDbContext dbContext) : IModifierGroupRepository
{
    public Task<ModifierGroup?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.ModifierGroups.FirstOrDefaultAsync(group => group.Id == id, cancellationToken);
    }

    public Task<ItemModifier?> GetModifierByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.ItemModifiers.FirstOrDefaultAsync(modifier => modifier.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<(ModifierGroup Group, IReadOnlyList<ItemModifier> Modifiers)>> ListByTenantWithModifiersAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var groups = await dbContext.ModifierGroups
            .AsNoTracking()
            .Where(group => group.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        var modifiers = await dbContext.ItemModifiers
            .AsNoTracking()
            .Where(modifier => modifier.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        var modifiersByGroup = modifiers.GroupBy(modifier => modifier.ModifierGroupId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<ItemModifier>)[.. group]);

        return groups
            .Select(group => (
                group,
                modifiersByGroup.TryGetValue(group.Id, out var groupModifiers)
                    ? groupModifiers
                    : []))
            .ToList();
    }

    public void Add(ModifierGroup group)
    {
        _ = dbContext.ModifierGroups.Add(group);
    }

    public void AddModifier(ItemModifier modifier)
    {
        _ = dbContext.ItemModifiers.Add(modifier);
    }
}
