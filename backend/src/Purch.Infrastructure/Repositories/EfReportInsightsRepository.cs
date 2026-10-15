using Microsoft.EntityFrameworkCore;
using Purch.Application.Reporting;
using Purch.Domain.Entities;
using Purch.Domain.Enums;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfReportInsightsRepository(PurchDbContext dbContext) : IReportInsightsRepository
{
    public async Task<IReadOnlyList<PaymentTotals>> GetPaymentTotalsAsync(
        Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default)
    {
        var rows = await (
                from payment in dbContext.Payments.AsNoTracking()
                join transaction in CompletedTransactions(branchId, fromUtc, toUtc) on payment.TransactionId equals transaction.Id
                where payment.Status == PaymentStatus.Confirmed
                group payment by payment.Method into byMethod
                select new { Method = byMethod.Key, Amount = byMethod.Sum(p => p.Amount), Count = byMethod.Count() })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(r => new PaymentTotals(r.Method, r.Amount, r.Count))];
    }

    public async Task<IReadOnlyList<CategoryTotals>> GetCategoryTotalsAsync(
        Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default)
    {
        var rows = await (
                from line in dbContext.TransactionLines.AsNoTracking()
                join transaction in CompletedTransactions(branchId, fromUtc, toUtc) on line.TransactionId equals transaction.Id
                join item in dbContext.Items.AsNoTracking() on line.ItemId equals item.Id into itemJoin
                from item in itemJoin.DefaultIfEmpty()
                group line by new { CategoryId = (Guid?)item.CategoryId, line.IsOpenItem } into byCategory
                select new
                {
                    byCategory.Key.CategoryId,
                    byCategory.Key.IsOpenItem,
                    Revenue = byCategory.Sum(l => l.LineTotal),
                    Quantity = byCategory.Sum(l => l.Quantity),
                })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(r => new CategoryTotals(r.CategoryId, r.IsOpenItem, r.Revenue, r.Quantity))];
    }

    public async Task<IReadOnlyList<UtcHourTotals>> GetUtcHourTotalsAsync(
        Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default)
    {
        // Project the instant first: grouping on the coalesced expression directly does not translate.
        var rows = await CompletedTransactions(branchId, fromUtc, toUtc)
            .Select(t => new { Instant = t.CompletedAt ?? t.CreatedAt, t.TotalAmount })
            .GroupBy(t => new
            {
                t.Instant.UtcDateTime.Year,
                t.Instant.UtcDateTime.Month,
                t.Instant.UtcDateTime.Day,
                t.Instant.UtcDateTime.Hour,
            })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, Sales = g.Sum(t => t.TotalAmount), Count = g.Count() })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(r => new UtcHourTotals(new DateOnly(r.Year, r.Month, r.Day), r.Hour, r.Sales, r.Count))];
    }

    public async Task<decimal> GetExchangePriceDifferenceAsync(
        Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Adjustments.AsNoTracking()
            .Where(a => a.CreatedAt >= fromUtc && a.CreatedAt < toUtc && a.PriceDifference > 0);
        if (branchId is { } id)
        {
            query = query.Where(a => a.BranchId == id);
        }

        return await query.SumAsync(a => (decimal?)a.PriceDifference, cancellationToken) ?? 0m;
    }

    private IQueryable<Transaction> CompletedTransactions(Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc)
    {
        var query = dbContext.Transactions.AsNoTracking()
            .Where(t => t.Status == TransactionStatus.Completed
                && (t.CompletedAt ?? t.CreatedAt) >= fromUtc
                && (t.CompletedAt ?? t.CreatedAt) < toUtc);
        return branchId is { } id ? query.Where(t => t.BranchId == id) : query;
    }
}
