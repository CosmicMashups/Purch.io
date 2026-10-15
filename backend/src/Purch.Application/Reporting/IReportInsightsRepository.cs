using Purch.Domain.Enums;

namespace Purch.Application.Reporting;

/// <summary>Aggregate-only queries for the U4 reports, summed in SQL and returned as small rows. Kept apart from
/// IReportingRepository so that interface stays about the older dashboard/export shapes.
///
/// "Sales" here are Completed and Refunded sales by completion time: gross, so a later refund never changes a
/// past day (refunds are reported by their own date). All time bucketing is by UTC hour; the services shift to
/// the UTC+8 business day, which is exact because the offset is a whole number of hours.</summary>
public interface IReportInsightsRepository
{
    Task<IReadOnlyList<PaymentTotals>> GetPaymentTotalsAsync(Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CategoryTotals>> GetCategoryTotalsAsync(Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ItemTotals>> GetItemTotalsAsync(Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default);

    /// <summary>Sale-level facts (totals, VAT stamps, discounts) per UTC hour.</summary>
    Task<IReadOnlyList<HourlySaleFacts>> GetHourlySaleFactsAsync(Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default);

    /// <summary>Line-level revenue/cost facts per UTC hour, for the margin trend.</summary>
    Task<IReadOnlyList<HourlyLineFacts>> GetHourlyLineFactsAsync(Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StaffDiscountTotals>> GetStaffDiscountTotalsAsync(Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ApproverCount>> GetApproverCountsAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HourlyEvent>> GetHourlyVoidsAsync(Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HourlyEvent>> GetHourlyRefundsAsync(Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default);

    /// <summary>Voided carts the report does not list: zero-value discards, and parked carts the retention sweep expired.</summary>
    Task<VoidExclusions> GetVoidExclusionsAsync(Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default);

    Task<decimal> GetExchangePriceDifferenceAsync(Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default);

    Task<RowPage<VoidRow>> ListVoidsAsync(Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, int offset, int limit, CancellationToken cancellationToken = default);

    Task<RowPage<RefundRow>> ListRefundsAsync(Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, int offset, int limit, CancellationToken cancellationToken = default);

    Task<RowPage<ExchangeRow>> ListExchangesAsync(Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, int offset, int limit, CancellationToken cancellationToken = default);

    Task<RowPage<ManualDiscountRow>> ListManualDiscountsAsync(Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, int offset, int limit, CancellationToken cancellationToken = default);
}

public sealed record RowPage<T>(IReadOnlyList<T> Rows, int Total);

public sealed record PaymentTotals(PaymentMethod Method, decimal Amount, int PaymentCount);

/// <summary><see cref="LineGross"/> is the sum of line totals; <see cref="KnownGross"/> and <see cref="Cost"/> only
/// cover lines whose sale-time cost snapshot is known (never treated as zero).</summary>
public sealed record CategoryTotals(Guid? CategoryId, bool IsOpenItem, decimal Revenue, decimal Quantity, decimal KnownGross, decimal Cost);

public sealed record ItemTotals(Guid ItemId, decimal Quantity, decimal LineGross, decimal KnownGross, decimal Cost);

public sealed record HourlySaleFacts(
    DateOnly UtcDate, int UtcHour, int SaleCount, decimal Total,
    decimal VatableSales, decimal VatAmount, decimal VatExemptSales, decimal ZeroRatedSales,
    int VatDerivedCount, int SeniorPwdCount, decimal SeniorPwdExemptAmount,
    decimal DiscountAmount, decimal PromoCodeDiscount, decimal ItemPromoDiscount, decimal SeniorPwdDiscount);

public sealed record HourlyLineFacts(DateOnly UtcDate, int UtcHour, decimal LineGross, decimal KnownGross, decimal Cost);

public sealed record StaffDiscountTotals(
    Guid? StaffUserId, int SaleCount, decimal Total, decimal DiscountAmount,
    decimal PromoCodeDiscount, decimal ItemPromoDiscount, decimal SeniorPwdDiscount);

public sealed record ApproverCount(Guid ApproverUserId, AuditActionType ActionType, int Count);

public sealed record HourlyEvent(DateOnly UtcDate, int UtcHour, int Count, decimal Amount);

public sealed record VoidExclusions(int EmptyDiscarded, int ExpiredParked);

public sealed record VoidRow(
    Guid TransactionId, DateTimeOffset VoidedAt, Guid BranchId, Guid DeviceId, Guid? StaffUserId, decimal Total,
    int LineCount, string? ParkedLabel, string? AfterStateJson, Guid? ApprovedByUserId, Guid ActorUserId);

public sealed record RefundRow(
    Guid TransactionId, long? ReceiptNumber, DateTimeOffset CompletedAt, DateTimeOffset RefundedAt, Guid BranchId,
    Guid? StaffUserId, decimal Total, string? AfterStateJson, Guid? ApprovedByUserId, Guid ActorUserId);

public sealed record ExchangeRow(
    Guid AdjustmentId, Guid OriginalTransactionId, long? OriginalReceiptNumber, DateTimeOffset CreatedAt, Guid BranchId,
    decimal ReturnedTotal, decimal ReplacementTotal, decimal PriceDifference, PaymentMethod? SettlementMethod,
    string Reason, Guid ApprovedByUserId);

public sealed record ManualDiscountRow(
    Guid TransactionId, long? ReceiptNumber, DateTimeOffset CompletedAt, Guid BranchId, Guid? StaffUserId, decimal Total,
    decimal CartDiscount, ManualDiscountKind? CartKind, decimal? CartValue, string? CartReason,
    decimal LineDiscounts, int LineDiscountCount, int PriceOverrideCount, Guid? ApprovedByUserId);
