using Microsoft.EntityFrameworkCore;
using Purch.Application.Inventory;
using Purch.Domain.Entities;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfItemRecipeRepository(PurchDbContext dbContext) : IItemRecipeRepository
{
    public async Task<IReadOnlyList<ItemRecipeLine>> ListByItemAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        return await dbContext.ItemRecipeLines
            .Where(line => line.ItemId == itemId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ItemRecipeLine>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.ItemRecipeLines
            .AsNoTracking()
            .Where(line => line.TenantId == tenantId)
            .ToListAsync(cancellationToken);
    }

    public void AddRange(IEnumerable<ItemRecipeLine> lines)
    {
        dbContext.ItemRecipeLines.AddRange(lines);
    }

    public void RemoveRange(IEnumerable<ItemRecipeLine> lines)
    {
        dbContext.ItemRecipeLines.RemoveRange(lines);
    }
}
