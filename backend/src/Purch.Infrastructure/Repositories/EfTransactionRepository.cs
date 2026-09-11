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
}
