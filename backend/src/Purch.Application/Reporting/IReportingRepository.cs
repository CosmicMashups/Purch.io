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

    /// <summary>Per-branch completed-sale revenue for the today / 7-day / 30-day windows, summed in SQL.
    /// <paramref name="toUtc"/> is exclusive; the 30-day window is the superset the query scans.</summary>
    Task<IReadOnlyList<BranchRevenueTotals>> GetBranchRevenueTotalsAsync(
        Guid? branchId,
        DateTimeOffset todayStart,
        DateTimeOffset last7Start,
        DateTimeOffset last30Start,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Completed-sale revenue per day for <paramref name="dayCount"/> consecutive days starting at
    /// <paramref name="firstDayStart"/>. Days with no sales are omitted.</summary>
    Task<IReadOnlyList<DayRevenue>> GetDailyRevenueAsync(
        Guid? branchId,
        DateTimeOffset firstDayStart,
        int dayCount,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ItemSalesTotals>> GetTopItemsByRevenueAsync(
        Guid? branchId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        int take,
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

public sealed record BranchRevenueTotals(Guid BranchId, decimal Today, decimal Last7Days, decimal Last30Days);

/// <summary><see cref="DayIndex"/> is zero-based from the first day requested.</summary>
public sealed record DayRevenue(int DayIndex, decimal Revenue);

public sealed record ItemSalesTotals(Guid ItemId, decimal Quantity, decimal Revenue);
