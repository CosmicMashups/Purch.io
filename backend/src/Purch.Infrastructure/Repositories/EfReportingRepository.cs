using Microsoft.EntityFrameworkCore;
using Purch.Application.Reporting;
using Purch.Domain.Entities;
using Purch.Domain.Enums;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfReportingRepository(PurchDbContext dbContext) : IReportingRepository
{
    public async Task<IReadOnlyList<Transaction>> ListCompletedTransactionsAsync(
        Guid? branchId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Transactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.Status == TransactionStatus.Completed
                && transaction.CreatedAt >= fromUtc
                && transaction.CreatedAt < toUtc);

        if (branchId is { } id)
        {
            query = query.Where(transaction => transaction.BranchId == id);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TransactionLine>> ListLinesForTransactionsAsync(
        IReadOnlyList<Guid> transactionIds,
        CancellationToken cancellationToken = default)
    {
        return transactionIds.Count == 0
            ? []
            : (IReadOnlyList<TransactionLine>)await dbContext.TransactionLines
            .AsNoTracking()
            .Where(line => transactionIds.Contains(line.TransactionId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BranchRevenueTotals>> GetBranchRevenueTotalsAsync(
        Guid? branchId,
        DateTimeOffset todayStart,
        DateTimeOffset last7Start,
        DateTimeOffset last30Start,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default)
    {
        var rows = await CompletedTransactions(branchId, last30Start, toUtc)
            .GroupBy(transaction => transaction.BranchId)
            .Select(group => new
            {
                BranchId = group.Key,
                Today = group.Sum(t => t.CreatedAt >= todayStart ? t.TotalAmount : 0m),
                Last7Days = group.Sum(t => t.CreatedAt >= last7Start ? t.TotalAmount : 0m),
                Last30Days = group.Sum(t => t.TotalAmount),
            })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(r => new BranchRevenueTotals(r.BranchId, r.Today, r.Last7Days, r.Last30Days))];
    }

    public async Task<IReadOnlyList<DayRevenue>> GetDailyRevenueAsync(
        Guid? branchId,
        DateTimeOffset firstDayStart,
        int dayCount,
        CancellationToken cancellationToken = default)
    {
        var rows = await CompletedTransactions(branchId, firstDayStart, firstDayStart.AddDays(dayCount))
            .GroupBy(transaction => (int)Math.Floor((transaction.CreatedAt - firstDayStart).TotalDays))
            .Select(group => new { DayIndex = group.Key, Revenue = group.Sum(t => t.TotalAmount) })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(r => new DayRevenue(r.DayIndex, r.Revenue))];
    }

    public async Task<IReadOnlyList<ItemSalesTotals>> GetTopItemsByRevenueAsync(
        Guid? branchId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        int take,
        CancellationToken cancellationToken = default)
    {
        var rows = await (
                from line in dbContext.TransactionLines.AsNoTracking()
                join transaction in CompletedTransactions(branchId, fromUtc, toUtc)
                    on line.TransactionId equals transaction.Id
                group line by line.ItemId into itemGroup
                orderby itemGroup.Sum(l => l.LineTotal) descending
                select new
                {
                    ItemId = itemGroup.Key,
                    Quantity = itemGroup.Sum(l => l.Quantity),
                    Revenue = itemGroup.Sum(l => l.LineTotal),
                })
            .Take(take)
            .ToListAsync(cancellationToken);

        return [.. rows.Select(r => new ItemSalesTotals(r.ItemId, r.Quantity, r.Revenue))];
    }

    private IQueryable<Transaction> CompletedTransactions(Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc)
    {
        var query = dbContext.Transactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.Status == TransactionStatus.Completed
                && transaction.CreatedAt >= fromUtc
                && transaction.CreatedAt < toUtc);

        return branchId is { } id ? query.Where(transaction => transaction.BranchId == id) : query;
    }

    public async Task<IReadOnlyList<InventoryMovement>> ListMovementsInRangeAsync(
        Guid? branchId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.InventoryMovements
            .AsNoTracking()
            .Where(movement => movement.CreatedAt >= fromUtc && movement.CreatedAt < toUtc);

        if (branchId is { } id)
        {
            query = query.Where(movement => movement.BranchId == id);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Shift>> ListClosedShiftsInRangeAsync(
        Guid? branchId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Shifts
            .AsNoTracking()
            .Where(shift =>
                shift.Status == ShiftStatus.Closed
                && shift.ClosedAt != null
                && shift.ClosedAt >= fromUtc
                && shift.ClosedAt < toUtc);

        if (branchId is { } id)
        {
            query = query.Where(shift => shift.BranchId == id);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DepartmentRevenueTotals>> GetDepartmentSalesAsync(
        Guid? branchId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default)
    {
        var rows = await (
                from line in dbContext.TransactionLines.AsNoTracking()
                join transaction in CompletedTransactions(branchId, fromUtc, toUtc)
                    on line.TransactionId equals transaction.Id
                join item in dbContext.Items.AsNoTracking()
                    on line.ItemId equals item.Id into itemJoin
                from item in itemJoin.DefaultIfEmpty()
                group line by item.DepartmentId into deptGroup
                select new
                {
                    DepartmentId = deptGroup.Key,
                    Revenue = deptGroup.Sum(l => l.LineTotal),
                })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(r => new DepartmentRevenueTotals(r.DepartmentId, r.Revenue))];
    }

    public async Task<IReadOnlyList<StaffSalesTotals>> GetStaffSalesAsync(
        Guid? branchId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default)
    {
        var rows = await CompletedTransactions(branchId, fromUtc, toUtc)
            .Where(t => t.StaffUserId != null)
            .GroupBy(t => t.StaffUserId!.Value)
            .Select(group => new
            {
                StaffUserId = group.Key,
                TransactionCount = group.Count(),
                TotalSales = group.Sum(t => t.TotalAmount),
            })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(r => new StaffSalesTotals(r.StaffUserId, r.TransactionCount, r.TotalSales))];
    }

    public async Task<IReadOnlyList<StaffAttendanceTotals>> GetStaffShiftAttendanceAsync(
        Guid? branchId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Shifts
            .AsNoTracking()
            .Where(shift =>
                shift.Status == ShiftStatus.Closed
                && shift.ClosedAt != null
                && shift.ClosedAt >= fromUtc
                && shift.ClosedAt < toUtc);

        if (branchId is { } id)
        {
            query = query.Where(shift => shift.BranchId == id);
        }

        var rows = await query
            .GroupBy(shift => shift.OpenedByUserId)
            .Select(group => new
            {
                StaffUserId = group.Key,
                ShiftsOpened = group.Count(),
                ShiftsWithDiscrepancy = group.Count(s => s.VarianceAmount != null && s.VarianceAmount != 0m),
            })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(r => new StaffAttendanceTotals(r.StaffUserId, r.ShiftsOpened, r.ShiftsWithDiscrepancy))];
    }
}
