using Microsoft.EntityFrameworkCore;
using Purch.Application.Pos;
using Purch.Domain.Entities;
using Purch.Domain.Enums;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfTransactionRepository(PurchDbContext dbContext) : ITransactionRepository
{
    public Task<Transaction?> GetOpenByDeviceAsync(Guid deviceId, CancellationToken cancellationToken = default)
    {
        return dbContext.Transactions
            .FirstOrDefaultAsync(transaction => transaction.DeviceId == deviceId && transaction.Status == TransactionStatus.Open, cancellationToken);
    }

    public Task<Transaction?> GetByClientSaleIdAsync(Guid clientSaleId, CancellationToken cancellationToken = default)
    {
        return dbContext.Transactions
            .FirstOrDefaultAsync(transaction => transaction.ClientSaleId == clientSaleId, cancellationToken);
    }

    public Task<bool> ReceiptNumberExistsAsync(Guid deviceId, long receiptNumber, CancellationToken cancellationToken = default)
    {
        return dbContext.Transactions
            .AnyAsync(transaction => transaction.DeviceId == deviceId && transaction.ReceiptNumber == receiptNumber, cancellationToken);
    }

    public Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.Transactions.FirstOrDefaultAsync(transaction => transaction.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<TransactionLine>> ListLinesAsync(Guid transactionId, CancellationToken cancellationToken = default)
    {
        return await dbContext.TransactionLines
            .Where(line => line.TransactionId == transactionId)
            .ToListAsync(cancellationToken);
    }

    public Task<TransactionLine?> GetLineAsync(Guid lineId, CancellationToken cancellationToken = default)
    {
        return dbContext.TransactionLines.FirstOrDefaultAsync(line => line.Id == lineId, cancellationToken);
    }

    public async Task<IReadOnlyList<TransactionLineComboSelection>> ListComboSelectionsAsync(Guid lineId, CancellationToken cancellationToken = default)
    {
        return await dbContext.TransactionLineComboSelections
            .Where(selection => selection.TransactionLineId == lineId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TransactionLineModifierSelection>> ListModifierSelectionsAsync(Guid lineId, CancellationToken cancellationToken = default)
    {
        return await dbContext.TransactionLineModifierSelections
            .Where(selection => selection.TransactionLineId == lineId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Transaction>> ListUnreportedCompletedByDeviceAsync(Guid deviceId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Transactions
            .Where(transaction =>
                transaction.DeviceId == deviceId
                && transaction.Status == TransactionStatus.Completed
                && transaction.ReceiptNumber != null
                && transaction.ZReadingNumber == null)
            .OrderBy(transaction => transaction.ReceiptNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Transaction>> ListVoidedByDeviceSinceAsync(Guid deviceId, DateTimeOffset since, CancellationToken cancellationToken = default)
    {
        return await dbContext.Transactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.DeviceId == deviceId
                && transaction.Status == TransactionStatus.Voided
                && transaction.CreatedAt >= since)
            .ToListAsync(cancellationToken);
    }

    public async Task<VoidedTotals> GetVoidedTotalsByDeviceSinceAsync(Guid deviceId, DateTimeOffset since, CancellationToken cancellationToken = default)
    {
        var row = await dbContext.Transactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.DeviceId == deviceId
                && transaction.Status == TransactionStatus.Voided
                && transaction.CreatedAt >= since)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Count = g.Count(),
                Amount = g.Sum(t => t.TotalAmount),
            })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null ? new VoidedTotals(0, 0m) : new VoidedTotals(row.Count, row.Amount);
    }

    public async Task<IReadOnlyList<Transaction>> ListPendingKioskOrdersByBranchAsync(Guid branchId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Transactions
            .Where(transaction =>
                transaction.BranchId == branchId
                && transaction.OriginatedFromKiosk
                && transaction.Status == TransactionStatus.AwaitingPayment)
            .OrderBy(transaction => transaction.KioskPrepNumber)
            .ToListAsync(cancellationToken);
    }

    public void Add(Transaction transaction)
    {
        _ = dbContext.Transactions.Add(transaction);
    }

    public void AddLine(TransactionLine line)
    {
        _ = dbContext.TransactionLines.Add(line);
    }

    public void RemoveLine(TransactionLine line)
    {
        _ = dbContext.TransactionLines.Remove(line);
    }

    public void AddComboSelection(TransactionLineComboSelection selection)
    {
        _ = dbContext.TransactionLineComboSelections.Add(selection);
    }

    public void AddModifierSelection(TransactionLineModifierSelection selection)
    {
        _ = dbContext.TransactionLineModifierSelections.Add(selection);
    }
}
