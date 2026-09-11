using Purch.Domain.Entities;

namespace Purch.Application.Pos;

public interface ITransactionRepository
{
    /// <summary>At most one Open transaction per device — a physical terminal can only ring up one cart at a time.</summary>
    Task<Transaction?> GetOpenByDeviceAsync(Guid deviceId, CancellationToken cancellationToken = default);

    Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TransactionLine>> ListLinesAsync(Guid transactionId, CancellationToken cancellationToken = default);

    Task<TransactionLine?> GetLineAsync(Guid lineId, CancellationToken cancellationToken = default);

    void Add(Transaction transaction);

    void AddLine(TransactionLine line);

    void RemoveLine(TransactionLine line);
}
