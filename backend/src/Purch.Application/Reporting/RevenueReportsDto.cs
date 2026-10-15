using Purch.Domain.Enums;

namespace Purch.Application.Reporting;

/// <summary>Range tax report. Reads the VAT columns stamped on each sale at checkout (not an on-the-fly recompute
/// like the X/Z reading), so a range total can differ from the sum of the X/Z readings by centavos: the stamps are
/// rounded per sale. <see cref="VatDerivedSaleCount"/> counts backfilled history stamped under the legacy 12% rule.
/// Senior/PWD exempt sales are stamped as Total + DiscountAmount, which includes any promo discount on that sale
/// (matches the legacy X/Z); treat it as best-effort until BIR accreditation.</summary>
public sealed record TaxReportDto(
    TaxDayDto Totals,
    IReadOnlyList<TaxDayDto> Days,
    int VatDerivedSaleCount,
    int SeniorPwdSaleCount,
    decimal SeniorPwdExemptAmount);

public sealed record TaxDayDto(
    DateOnly Date,
    int SaleCount,
    decimal GrossSales,
    decimal VatableSales,
    decimal VatAmount,
    decimal VatExemptSales,
    decimal ZeroRatedSales);

/// <summary>Margin over the lines whose sale-time cost is known. Revenue is net of discounts and ex-VAT (the stamped
/// vatable + exempt + zero-rated sales) and is attributed to lines in proportion to their line totals, so
/// sale-level discounts spread evenly across a sale's lines; unknown-cost and open-item lines are excluded and
/// reported as <see cref="UnknownCostRevenue"/>. A null cost is never read as zero.</summary>
public sealed record ProfitReportDto(
    decimal RevenueExVat,
    decimal KnownCostRevenue,
    decimal UnknownCostRevenue,
    decimal Cost,
    decimal GrossProfit,
    decimal? MarginPercent,
    decimal? CoveragePercent,
    IReadOnlyList<ProfitDayDto> Days,
    IReadOnlyList<ProfitGroupDto> Categories,
    IReadOnlyList<ProfitGroupDto> TopItems,
    IReadOnlyList<ProfitGroupDto> BottomItems);

public sealed record ProfitDayDto(DateOnly Date, decimal KnownCostRevenue, decimal Cost, decimal GrossProfit, decimal? MarginPercent, decimal? CoveragePercent);

public sealed record ProfitGroupDto(Guid? Id, string Name, decimal Quantity, decimal KnownCostRevenue, decimal Cost, decimal GrossProfit, decimal? MarginPercent, decimal? CoveragePercent);

public sealed record DiscountReportDto(
    decimal GrossSales,
    decimal PromoCodeDiscount,
    decimal ItemPromoDiscount,
    decimal ManualDiscount,
    decimal SeniorPwdDiscount,
    decimal TotalDiscounts,
    decimal DiscountRatePercent,
    IReadOnlyList<DiscountByStaffDto> ByCashier,
    IReadOnlyList<DiscountApproverDto> Approvers);

/// <summary>Rate is discounts as a share of sales before discounts (net sales + discounts).</summary>
public sealed record DiscountByStaffDto(Guid? StaffUserId, string Name, int SaleCount, decimal Sales, decimal Discounts, decimal RatePercent);

public sealed record DiscountApproverDto(Guid UserId, string Name, int DiscountApprovals, int PriceOverrideApprovals);

public sealed record ManualDiscountRowDto(
    Guid TransactionId,
    long? ReceiptNumber,
    DateTimeOffset CompletedAt,
    string BranchName,
    string StaffName,
    decimal Total,
    decimal CartDiscount,
    ManualDiscountKind? CartKind,
    decimal? CartValue,
    decimal LineDiscounts,
    int LineDiscountCount,
    int PriceOverrideCount,
    string? Reason,
    string? ApproverName);

public sealed record PageDto<T>(IReadOnlyList<T> Rows, int Total, int Offset, int Limit);

/// <summary>Voids of carts that had value, by staff or approved discard. Zero-value discards and parked carts the
/// retention sweep expired are excluded from the list and totals and reported as counts, so the totals can differ
/// from the X/Z voided count (which does include empty discards).</summary>
public sealed record VoidReportDto(
    int Count,
    decimal Amount,
    int EmptyDiscardedCount,
    int ExpiredParkedCount,
    PageDto<VoidRowDto> Voids);

public sealed record VoidRowDto(
    Guid TransactionId,
    DateTimeOffset VoidedAt,
    string BranchName,
    string StaffName,
    decimal Total,
    int LineCount,
    string? ParkedLabel,
    string? Reason,
    string? ApproverName);

/// <summary>Refunds by the date the refund happened, with exchange adjustments listed beside them.</summary>
public sealed record RefundReportDto(
    int RefundCount,
    decimal RefundAmount,
    decimal ExchangePriceDifference,
    PageDto<RefundRowDto> Refunds,
    PageDto<ExchangeRowDto> Exchanges);

public sealed record RefundRowDto(
    Guid TransactionId,
    long? ReceiptNumber,
    DateTimeOffset CompletedAt,
    DateTimeOffset RefundedAt,
    string BranchName,
    string StaffName,
    decimal Total,
    string? Reason,
    string? ApproverName);

public sealed record ExchangeRowDto(
    Guid AdjustmentId,
    Guid OriginalTransactionId,
    long? OriginalReceiptNumber,
    DateTimeOffset CreatedAt,
    string BranchName,
    decimal ReturnedTotal,
    decimal ReplacementTotal,
    decimal PriceDifference,
    PaymentMethod? SettlementMethod,
    string Reason,
    string ApproverName);

/// <summary>The three executive dashboard series in one response: margin trend, revenue leakage, category mix.</summary>
public sealed record DashboardInsightsDto(
    IReadOnlyList<ProfitDayDto> MarginTrend,
    decimal? MarginPercent,
    decimal? CoveragePercent,
    IReadOnlyList<LeakageDayDto> Leakage,
    decimal LeakagePercent,
    IReadOnlyList<CategoryShareDto> Categories);

/// <summary>Discounts + voids + refunds against sales before discounts for one business day.</summary>
public sealed record LeakageDayDto(DateOnly Date, decimal GrossSales, decimal Discounts, decimal Voids, decimal Refunds, decimal LeakagePercent);

public sealed record CategoryShareDto(string Name, bool IsOpenItem, decimal Revenue, decimal SharePercent, decimal? MarginPercent);
