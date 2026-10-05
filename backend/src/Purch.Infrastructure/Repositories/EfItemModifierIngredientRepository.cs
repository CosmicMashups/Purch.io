using Microsoft.EntityFrameworkCore;
using Purch.Application.Catalog;
using Purch.Domain.Entities;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfItemModifierIngredientRepository(PurchDbContext dbContext) : IItemModifierIngredientRepository
{
    public async Task<IReadOnlyList<ItemModifierIngredient>> ListByModifierAsync(Guid modifierId, CancellationToken cancellationToken = default)
    {
        return await dbContext.ItemModifierIngredients
            .Where(ingredient => ingredient.ItemModifierId == modifierId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ItemModifierIngredient>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.ItemModifierIngredients
            .AsNoTracking()
            .Where(ingredient => ingredient.TenantId == tenantId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ItemModifierIngredient>> ListByModifiersAsync(IReadOnlyCollection<Guid> modifierIds, CancellationToken cancellationToken = default)
    {
        if (modifierIds.Count == 0)
        {
            return [];
        }

        return await dbContext.ItemModifierIngredients
            .AsNoTracking()
            .Where(ingredient => modifierIds.Contains(ingredient.ItemModifierId))
            .ToListAsync(cancellationToken);
    }

    public void AddRange(IEnumerable<ItemModifierIngredient> ingredients)
    {
        dbContext.ItemModifierIngredients.AddRange(ingredients);
    }

    public void RemoveRange(IEnumerable<ItemModifierIngredient> ingredients)
    {
        dbContext.ItemModifierIngredients.RemoveRange(ingredients);
    }
}
