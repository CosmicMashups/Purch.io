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

    public Task<StockTake?> GetUntrackedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.StockTakes
            .AsNoTracking()
            .Include(take => take.Lines)
            .FirstOrDefaultAsync(take => take.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<StockTakeSummaryDto>> ListSummariesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.StockTakes
            .AsNoTracking()
            .Where(take => take.TenantId == tenantId)
            .OrderByDescending(take => take.CreatedAt)
            .Select(take => new StockTakeSummaryDto(
                take.Id,
                take.Name,
                take.Status,
                take.Blind,
                take.ScopeKind,
                take.CreatedAt,
                take.AppliedAt,
                take.Lines.Count,
                take.Lines.Count(line => line.State == StockTakeLineState.Counted)))
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

    public async Task<IReadOnlyList<InventoryMovement>> ListMovementsSinceAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> itemIds,
        IReadOnlyCollection<Guid> inventoryItemIds,
        DateTimeOffset since,
        CancellationToken cancellationToken = default)
    {
        if (itemIds.Count + inventoryItemIds.Count == 0)
        {
            return [];
        }

        // No branch filter on purpose: stock is tenant-wide, so a sale at any branch moves the counted quantity.
        return await dbContext.InventoryMovements
            .AsNoTracking()
            .Where(movement => movement.TenantId == tenantId
                && movement.CreatedAt > since
                && ((movement.ItemId != null && itemIds.Contains(movement.ItemId.Value))
                    || (movement.InventoryItemId != null && inventoryItemIds.Contains(movement.InventoryItemId.Value))))
            .ToListAsync(cancellationToken);
    }

    public void Add(StockTake stockTake)
    {
        _ = dbContext.StockTakes.Add(stockTake);
    }
}
