using Microsoft.EntityFrameworkCore;
using Purch.Application.Catalog;
using Purch.Domain.Entities;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfItemBatchRepository(PurchDbContext dbContext) : IItemBatchRepository
{
    public async Task<IReadOnlyList<ItemBatch>> ListByItemAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        return await dbContext.ItemBatches
            .AsNoTracking()
            .Where(batch => batch.ItemId == itemId)
            .OrderBy(batch => batch.ReceivedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ItemBatch>> ListConsumableAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        return await dbContext.ItemBatches
            .Where(batch => batch.ItemId == itemId && batch.QuantityRemaining > 0)
            .OrderBy(batch => batch.ExpiryDate == null)
            .ThenBy(batch => batch.ExpiryDate)
            .ThenBy(batch => batch.ReceivedAt)
            .ToListAsync(cancellationToken);
    }

    public void Add(ItemBatch batch)
    {
        _ = dbContext.ItemBatches.Add(batch);
    }
}
