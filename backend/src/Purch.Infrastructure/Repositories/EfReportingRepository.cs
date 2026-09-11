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
}
