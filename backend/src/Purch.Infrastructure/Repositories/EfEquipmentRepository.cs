using Microsoft.EntityFrameworkCore;
using Purch.Application.EquipmentInventory;
using Purch.Domain.Entities;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfEquipmentRepository(PurchDbContext dbContext) : IEquipmentRepository
{
    public async Task<IReadOnlyList<Equipment>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.EquipmentItems
            .AsNoTracking()
            .Where(equipment => equipment.TenantId == tenantId && !equipment.IsDeleted)
            .ToListAsync(cancellationToken);
    }

    public Task<Equipment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.EquipmentItems.FirstOrDefaultAsync(equipment => equipment.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Equipment>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        return ids.Count == 0
            ? []
            : (IReadOnlyList<Equipment>)await dbContext.EquipmentItems
                .AsNoTracking()
                .Where(equipment => ids.Contains(equipment.Id))
                .ToListAsync(cancellationToken);
    }

    public void Add(Equipment equipment)
    {
        _ = dbContext.EquipmentItems.Add(equipment);
    }
}

public sealed class EfItemEquipmentRepository(PurchDbContext dbContext) : IItemEquipmentRepository
{
    public async Task<IReadOnlyList<ItemEquipment>> ListByItemAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        return await dbContext.ItemEquipmentLinks
            .Where(link => link.ItemId == itemId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ItemEquipment>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.ItemEquipmentLinks
            .AsNoTracking()
            .Where(link => link.TenantId == tenantId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ItemEquipment>> ListByItemsAsync(IReadOnlyCollection<Guid> itemIds, CancellationToken cancellationToken = default)
    {
        return itemIds.Count == 0
            ? []
            : (IReadOnlyList<ItemEquipment>)await dbContext.ItemEquipmentLinks
                .AsNoTracking()
                .Where(link => itemIds.Contains(link.ItemId))
                .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> CountItemsByEquipmentAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        // Join to Items so a deleted item no longer counts as depending on the equipment.
        return await (from link in dbContext.ItemEquipmentLinks.AsNoTracking()
                      join item in dbContext.Items on link.ItemId equals item.Id
                      where link.TenantId == tenantId && !item.IsDeleted
                      group link by link.EquipmentId into grouped
                      select new { grouped.Key, Count = grouped.Count() })
            .ToDictionaryAsync(row => row.Key, row => row.Count, cancellationToken);
    }

    public void AddRange(IEnumerable<ItemEquipment> links)
    {
        dbContext.ItemEquipmentLinks.AddRange(links);
    }

    public void RemoveRange(IEnumerable<ItemEquipment> links)
    {
        dbContext.ItemEquipmentLinks.RemoveRange(links);
    }
}
