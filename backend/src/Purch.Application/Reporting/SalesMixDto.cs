using Purch.Domain.Enums;

namespace Purch.Application.Reporting;

/// <summary>One response backs the whole Sales mix tab: payment type, category and hour all come from the same
/// completed sales, so the sub-views switch client-side with no further request. Gross: later refunds do not
/// reduce these figures (they are reported by refund date in the refund report).</summary>
public sealed record SalesMixDto(
    decimal GrossSales,
    int TransactionCount,
    int OperatingDays,
    IReadOnlyList<PaymentMixRowDto> Payments,
    decimal ExchangeSettlements,
    IReadOnlyList<CategoryMixRowDto> Categories,
    IReadOnlyList<HourRowDto> Hours,
    IReadOnlyList<HeatCellDto> Heatmap);

/// <summary>Confirmed tender rows summed by method (a split sale contributes one row per part; Amount is the
/// applied amount, so cash is already net of change). UtangCredit is credit extended, not cash received.</summary>
public sealed record PaymentMixRowDto(PaymentMethod Method, decimal Amount, int PaymentCount, bool IsCredit);

/// <summary>CategoryId null with IsOpenItem false is "Uncategorised". Lines carry no category snapshot, so this
/// follows each item's current category.</summary>
public sealed record CategoryMixRowDto(Guid? CategoryId, string Name, bool IsOpenItem, decimal Revenue, decimal Quantity);

/// <summary>Hour 0-23 in UTC+8 business time.</summary>
public sealed record HourRowDto(int Hour, decimal Sales, int TransactionCount, decimal AverageBasket, decimal AveragePerOperatingDay);

/// <summary>DayOfWeek 0 = Sunday, in UTC+8 business time.</summary>
public sealed record HeatCellDto(int DayOfWeek, int Hour, decimal Sales, int TransactionCount);
