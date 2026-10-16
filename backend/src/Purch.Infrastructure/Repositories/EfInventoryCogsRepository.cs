using Microsoft.EntityFrameworkCore;
using Purch.Application.Reporting;
using Purch.Domain.Enums;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfInventoryCogsRepository(PurchDbContext dbContext) : IInventoryCogsRepository
{
    public async Task<IReadOnlyList<CogsAggregateRow>> ListAggregatesAsync(
        DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default)
    {
        var returned = dbContext.AdjustmentReturnLines.AsNoTracking()
            .GroupBy(r => r.OriginalLineId)
            .Select(g => new { LineId = g.Key, Quantity = g.Sum(r => r.Quantity) });

        // One row per sold line with what stayed sold; the grouping below runs in the database.
        var lines =
            from line in dbContext.TransactionLines.AsNoTracking()
            join t in dbContext.Transactions.AsNoTracking() on line.TransactionId equals t.Id
            join item in dbContext.Items.AsNoTracking() on line.ItemId equals item.Id into itemJoin
            from item in itemJoin.DefaultIfEmpty()
            join r in returned on line.Id equals r.LineId into returnJoin
            from r in returnJoin.DefaultIfEmpty()
            where (t.Status == TransactionStatus.Completed || t.Status == TransactionStatus.Refunded)
                && (t.CompletedAt ?? t.CreatedAt) >= fromUtc
                && (t.CompletedAt ?? t.CreatedAt) < toUtc
                && line.Quantity > 0m
            let net = line.Quantity - (t.Status == TransactionStatus.Refunded ? line.Quantity : (decimal?)r.Quantity ?? 0m)
            where net > 0m
            select new
            {
                // Business days are UTC+8.
                Day = (t.CompletedAt ?? t.CreatedAt).AddHours(8).Date,
                line.ItemId,
                CategoryId = (Guid?)item.CategoryId,
                line.IsOpenItem,
                Net = net,
                Revenue = line.LineTotal * net / line.Quantity,
                Known = line.UnitCost != null && !line.IsOpenItem,
                Cost = line.UnitCost ?? 0m,
            };

        var rows = await lines
            .GroupBy(x => new { x.Day, x.ItemId, x.CategoryId, x.IsOpenItem })
            .Select(g => new
            {
                g.Key.Day,
                g.Key.ItemId,
                g.Key.CategoryId,
                g.Key.IsOpenItem,
                Quantity = g.Sum(x => x.Net),
                Revenue = g.Sum(x => x.Revenue),
                Cogs = g.Sum(x => x.Known ? x.Net * x.Cost : 0m),
                KnownRevenue = g.Sum(x => x.Known ? x.Revenue : 0m),
                Excluded = g.Count(x => !x.Known),
            })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(x => new CogsAggregateRow(
            x.Day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            x.ItemId, x.CategoryId, x.IsOpenItem, x.Quantity, x.Revenue, x.Cogs, x.KnownRevenue, x.Excluded))];
    }
}
