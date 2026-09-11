using Purch.Domain.Entities;

namespace Purch.Application.Pos;

public interface ITransactionRepository
{
    /// <summary>At most one Open transaction per device — a physical terminal can only ring up one cart at a time.</summary>
    Task<Transaction?> GetOpenByDeviceAsync(Guid deviceId, CancellationToken cancellationToken = default);

    Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TransactionLine>> ListLinesAsync(Guid transactionId, CancellationToken cancellationToken = default);

    Task<TransactionLine?> GetLineAsync(Guid lineId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TransactionLineComboSelection>> ListComboSelectionsAsync(Guid lineId, CancellationToken cancellationToken = default);

    /// <summary>Completed sales on this device with ReceiptNumber greater than the given number, ordered ascending — the range a Z/X-reading covers.</summary>
    Task<IReadOnlyList<Transaction>> ListCompletedByDeviceInReceiptRangeAsync(Guid deviceId, long fromReceiptNumberExclusive, CancellationToken cancellationToken = default);

    /// <summary>Voided carts on this device since the given time — voided carts never get a receipt number, so they can't be selected by range.</summary>
    Task<IReadOnlyList<Transaction>> ListVoidedByDeviceSinceAsync(Guid deviceId, DateTimeOffset since, CancellationToken cancellationToken = default);

    /// <summary>Kiosk orders submitted at this branch, awaiting pickup by a cashier — see TransactionService.SubmitKioskOrderAsync/ClaimKioskOrderAsync.</summary>
    Task<IReadOnlyList<Transaction>> ListPendingKioskOrdersByBranchAsync(Guid branchId, CancellationToken cancellationToken = default);

    void Add(Transaction transaction);

    void AddLine(TransactionLine line);

    void RemoveLine(TransactionLine line);

    void AddComboSelection(TransactionLineComboSelection selection);
}
