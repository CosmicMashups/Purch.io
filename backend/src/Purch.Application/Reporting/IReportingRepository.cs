using Purch.Domain.Entities;

namespace Purch.Application.Reporting;

/// <summary>Read-only queries backing F1/F3/F4 — deliberately separate from
/// ITransactionRepository/IInventoryMovementRepository, which are shaped
/// around the cart/movement-log write paths, not report aggregation. The
/// tenant filter is applied automatically by PurchDbContext's global query
/// filter, same as every other repository in this codebase.</summary>
public interface IReportingRepository
{
    Task<IReadOnlyList<Transaction>> ListCompletedTransactionsAsync(
        Guid? branchId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TransactionLine>> ListLinesForTransactionsAsync(
        IReadOnlyList<Guid> transactionIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InventoryMovement>> ListMovementsInRangeAsync(
        Guid? branchId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Shift>> ListClosedShiftsInRangeAsync(
        Guid? branchId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default);
}
