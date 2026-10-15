using Microsoft.EntityFrameworkCore;
using Purch.Application.Reporting;
using Purch.Domain.Enums;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfInventoryCogsRepository(PurchDbContext dbContext) : IInventoryCogsRepository
{
    public async Task<IReadOnlyList<CogsLineRow>> ListLinesAsync(
        DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default)
    {
        var returned = dbContext.AdjustmentReturnLines.AsNoTracking()
            .GroupBy(r => r.OriginalLineId)
            .Select(g => new { LineId = g.Key, Quantity = g.Sum(r => r.Quantity) });

        var rows = await (
                from line in dbContext.TransactionLines.AsNoTracking()
                join t in dbContext.Transactions.AsNoTracking() on line.TransactionId equals t.Id
                join item in dbContext.Items.AsNoTracking() on line.ItemId equals item.Id into itemJoin
                from item in itemJoin.DefaultIfEmpty()
                join r in returned on line.Id equals r.LineId into returnJoin
                from r in returnJoin.DefaultIfEmpty()
                where (t.Status == TransactionStatus.Completed || t.Status == TransactionStatus.Refunded)
                    && (t.CompletedAt ?? t.CreatedAt) >= fromUtc
                    && (t.CompletedAt ?? t.CreatedAt) < toUtc
                select new
                {
                    SoldAt = t.CompletedAt ?? t.CreatedAt,
                    line.ItemId,
                    CategoryId = (Guid?)item.CategoryId,
                    line.IsOpenItem,
                    line.Quantity,
                    Returned = t.Status == TransactionStatus.Refunded ? line.Quantity : (decimal?)r.Quantity ?? 0m,
                    line.LineTotal,
                    line.UnitCost,
                })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(x => new CogsLineRow(x.SoldAt, x.ItemId, x.CategoryId, x.IsOpenItem, x.Quantity, x.Returned, x.LineTotal, x.UnitCost))];
    }
}
