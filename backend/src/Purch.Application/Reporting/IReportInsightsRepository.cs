using Purch.Domain.Enums;

namespace Purch.Application.Reporting;

/// <summary>Aggregate-only queries for the U4 reports, summed in SQL and returned as small rows. Kept apart from
/// IReportingRepository so that interface stays about the older dashboard/export shapes.</summary>
public interface IReportInsightsRepository
{
    Task<IReadOnlyList<PaymentTotals>> GetPaymentTotalsAsync(Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CategoryTotals>> GetCategoryTotalsAsync(Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default);

    /// <summary>Completed sales grouped by UTC calendar date and UTC hour; the caller shifts to business time.</summary>
    Task<IReadOnlyList<UtcHourTotals>> GetUtcHourTotalsAsync(Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default);

    Task<decimal> GetExchangePriceDifferenceAsync(Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default);
}

public sealed record PaymentTotals(PaymentMethod Method, decimal Amount, int PaymentCount);

public sealed record CategoryTotals(Guid? CategoryId, bool IsOpenItem, decimal Revenue, decimal Quantity);

public sealed record UtcHourTotals(DateOnly UtcDate, int UtcHour, decimal Sales, int TransactionCount);
