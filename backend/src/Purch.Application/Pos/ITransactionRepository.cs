using Purch.Domain.Entities;

namespace Purch.Application.Pos;

public interface ITransactionRepository
{
    /// <summary>At most one Open transaction per device — a physical terminal can only ring up one cart at a time.</summary>
    Task<Transaction?> GetOpenByDeviceAsync(Guid deviceId, CancellationToken cancellationToken = default);

    /// <summary>The transaction created for a checkout's client-generated SaleId, if any — the idempotency lookup.</summary>
    Task<Transaction?> GetByClientSaleIdAsync(Guid clientSaleId, CancellationToken cancellationToken = default);

    /// <summary>Whether a completed sale on this terminal already carries this receipt number.</summary>
    Task<bool> ReceiptNumberExistsAsync(Guid deviceId, long receiptNumber, CancellationToken cancellationToken = default);

    Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Completed sales across the tenant carrying this receipt number — for looking up an older sale
    /// to refund or exchange. Receipt numbers are only unique per device, so more than one device's sale can
    /// share a number; ordered most recent first so the common case (one match) needs no further choice.</summary>
    Task<IReadOnlyList<Transaction>> ListCompletedByReceiptNumberAsync(Guid tenantId, long receiptNumber, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TransactionLine>> ListLinesAsync(Guid transactionId, CancellationToken cancellationToken = default);

    Task<TransactionLine?> GetLineAsync(Guid lineId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TransactionLineComboSelection>> ListComboSelectionsAsync(Guid lineId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TransactionLineModifierSelection>> ListModifierSelectionsAsync(Guid lineId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TransactionLineComboSelection>> ListComboSelectionsByLinesAsync(IReadOnlyCollection<Guid> lineIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TransactionLineModifierSelection>> ListModifierSelectionsByLinesAsync(IReadOnlyCollection<Guid> lineIds, CancellationToken cancellationToken = default);

    /// <summary>Every completed sale on this device that no Z-reading has reported yet, ordered by receipt number —
    /// what an X/Z-reading covers. Tracked, so a Z-reading can stamp them as reported in the same save as its counters.</summary>
    Task<IReadOnlyList<Transaction>> ListUnreportedCompletedByDeviceAsync(Guid deviceId, CancellationToken cancellationToken = default);

    /// <summary>Voided carts on this device since the given time — voided carts never get a receipt number, so they can't be selected by range.</summary>
    Task<IReadOnlyList<Transaction>> ListVoidedByDeviceSinceAsync(Guid deviceId, DateTimeOffset since, CancellationToken cancellationToken = default);

    Task<VoidedTotals> GetVoidedTotalsByDeviceSinceAsync(Guid deviceId, DateTimeOffset since, CancellationToken cancellationToken = default);

    /// <summary>Sales originally rung up on this device that were refunded since the given time — filtered
    /// by RefundedAt, not CreatedAt, so a refund of an old (even already-reported) sale still lands in the
    /// reading whose window it actually happened in. See BirReadingDto.RefundsTotal.</summary>
    Task<RefundedTotals> GetRefundedTotalsByDeviceSinceAsync(Guid deviceId, DateTimeOffset since, CancellationToken cancellationToken = default);

    /// <summary>Kiosk orders submitted at this branch, awaiting pickup by a cashier — see TransactionService.SubmitKioskOrderAsync/ClaimKioskOrderAsync.</summary>
    Task<IReadOnlyList<Transaction>> ListPendingKioskOrdersByBranchAsync(Guid branchId, CancellationToken cancellationToken = default);

    /// <summary>Whether a batch of cart adds with this id was already applied.</summary>
    Task<bool> BatchReceiptExistsAsync(Guid batchId, CancellationToken cancellationToken = default);

    void AddBatchReceipt(CartBatchReceipt receipt);

    void Add(Transaction transaction);

    void AddLine(TransactionLine line);

    void RemoveLine(TransactionLine line);

    void AddComboSelection(TransactionLineComboSelection selection);

    void AddModifierSelection(TransactionLineModifierSelection selection);
}

public sealed record VoidedTotals(int Count, decimal Amount);

public sealed record RefundedTotals(int Count, decimal Amount);
