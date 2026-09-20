using Microsoft.EntityFrameworkCore;
using Purch.Application.Inventory;
using Purch.Domain.Entities;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfInventoryItemRepository(PurchDbContext dbContext) : IInventoryItemRepository
{
    public async Task<IReadOnlyList<InventoryItem>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.InventoryItems
            .AsNoTracking()
            .Where(inventoryItem => inventoryItem.TenantId == tenantId)
            .ToListAsync(cancellationToken);
    }

    public Task<InventoryItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.InventoryItems.FirstOrDefaultAsync(inventoryItem => inventoryItem.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<InventoryItem>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        return ids.Count == 0
            ? []
            : (IReadOnlyList<InventoryItem>)await dbContext.InventoryItems
            .AsNoTracking()
            .Where(inventoryItem => ids.Contains(inventoryItem.Id))
            .ToListAsync(cancellationToken);
    }

    public Task<InventoryItem?> GetByLinkedItemIdAsync(Guid tenantId, Guid itemId, CancellationToken cancellationToken = default)
    {
        return dbContext.InventoryItems.FirstOrDefaultAsync(
            inventoryItem => inventoryItem.TenantId == tenantId && inventoryItem.LinkedItemId == itemId,
            cancellationToken);
    }

    public void Add(InventoryItem inventoryItem)
    {
        _ = dbContext.InventoryItems.Add(inventoryItem);
    }
}
