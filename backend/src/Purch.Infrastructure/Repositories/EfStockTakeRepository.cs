using Microsoft.EntityFrameworkCore;
using Purch.Application.Inventory;
using Purch.Domain.Entities;
using Purch.Domain.Enums;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfStockTakeRepository(PurchDbContext dbContext) : IStockTakeRepository
{
    public Task<StockTake?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.StockTakes
            .Include(take => take.Lines)
            .FirstOrDefaultAsync(take => take.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<StockTake>> ListAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.StockTakes
            .AsNoTracking()
            .Include(take => take.Lines)
            .Where(take => take.TenantId == tenantId)
            .OrderByDescending(take => take.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<(Guid? ItemId, Guid? InventoryItemId)>> ListOpenLineTargetsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var rows = await (
                from line in dbContext.StockTakeLines.AsNoTracking()
                join take in dbContext.StockTakes.AsNoTracking() on line.StockTakeId equals take.Id
                where take.TenantId == tenantId
                    && (take.Status == StockTakeStatus.Draft || take.Status == StockTakeStatus.Counting || take.Status == StockTakeStatus.Review)
                select new { line.ItemId, line.InventoryItemId })
            .ToListAsync(cancellationToken);
        return rows.Select(row => (row.ItemId, row.InventoryItemId)).ToList();
    }

    public void Add(StockTake stockTake)
    {
        _ = dbContext.StockTakes.Add(stockTake);
    }
}
