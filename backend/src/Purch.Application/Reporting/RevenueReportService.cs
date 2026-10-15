using System.Text.Json;
using Purch.Application.Auth;
using Purch.Application.Catalog;
using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Application.Onboarding;
using Purch.Domain.Enums;

namespace Purch.Application.Reporting;

public interface IRevenueReportService
{
    Task<TaxReportDto> GetTaxAsync(Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default);

    Task<ProfitReportDto> GetProfitAsync(Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default);

    Task<DiscountReportDto> GetDiscountsAsync(Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default);

    Task<PageDto<ManualDiscountRowDto>> ListManualDiscountsAsync(Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, int offset, int limit, CancellationToken cancellationToken = default);

    Task<VoidReportDto> GetVoidsAsync(Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, int offset, int limit, CancellationToken cancellationToken = default);

    Task<RefundReportDto> GetRefundsAsync(Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, int offset, int limit, CancellationToken cancellationToken = default);

    Task<DashboardInsightsDto> GetInsightsAsync(Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default);

    Task<string> ExportVoidsCsvAsync(Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default);

    Task<string> ExportRefundsCsvAsync(Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default);

    Task<string> ExportManualDiscountsCsvAsync(Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default);
}

public sealed class RevenueReportService(
    IReportInsightsRepository repository,
    IItemRepository itemRepository,
    ICategoryRepository categoryRepository,
    IBranchRepository branchRepository,
    IUserRepository userRepository,
    IReportScopeResolver reportScopeResolver,
    ICurrentTenantProvider currentTenantProvider) : IRevenueReportService
{
    private const int MaxPageSize = 200;
    private const int RankedItems = 10;
    private const int DashboardCategories = 6;

    public async Task<TaxReportDto> GetTaxAsync(Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default)
    {
        var scopedBranchId = await ScopeAsync(branchId, fromUtc, toUtc, cancellationToken);
        var facts = await repository.GetHourlySaleFactsAsync(scopedBranchId, fromUtc, toUtc, cancellationToken);

        var days = LocalDays(facts, f => (f.UtcDate, f.UtcHour))
            .Select(day => new TaxDayDto(
                day.Key,
                day.Sum(f => f.SaleCount),
                day.Sum(f => f.Total),
                day.Sum(f => f.VatableSales),
                day.Sum(f => f.VatAmount),
                day.Sum(f => f.VatExemptSales),
                day.Sum(f => f.ZeroRatedSales)))
            .ToList();

        var totals = new TaxDayDto(
            default,
            days.Sum(d => d.SaleCount),
            days.Sum(d => d.GrossSales),
            days.Sum(d => d.VatableSales),
            days.Sum(d => d.VatAmount),
            days.Sum(d => d.VatExemptSales),
            days.Sum(d => d.ZeroRatedSales));

        return new TaxReportDto(
            totals,
            days,
            facts.Sum(f => f.VatDerivedCount),
            facts.Sum(f => f.SeniorPwdCount),
            facts.Sum(f => f.SeniorPwdExemptAmount));
    }

    public async Task<ProfitReportDto> GetProfitAsync(Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default)
    {
        var scopedBranchId = await ScopeAsync(branchId, fromUtc, toUtc, cancellationToken);
        var saleFacts = await repository.GetHourlySaleFactsAsync(scopedBranchId, fromUtc, toUtc, cancellationToken);
        var lineFacts = await repository.GetHourlyLineFactsAsync(scopedBranchId, fromUtc, toUtc, cancellationToken);
        var categoryTotals = await repository.GetCategoryTotalsAsync(scopedBranchId, fromUtc, toUtc, cancellationToken);
        var itemTotals = await repository.GetItemTotalsAsync(scopedBranchId, fromUtc, toUtc, cancellationToken);

        var days = ProfitDays(saleFacts, lineFacts);
        var revenueExVat = saleFacts.Sum(RevenueExVat);
        var lineGross = lineFacts.Sum(f => f.LineGross);
        var knownGross = lineFacts.Sum(f => f.KnownGross);
        var cost = lineFacts.Sum(f => f.Cost);
        var knownRevenue = Allocate(revenueExVat, knownGross, lineGross);

        // Sale-level discounts are not on the lines, so one range-wide factor turns line totals into net ex-VAT revenue.
        var factor = lineGross > 0m ? revenueExVat / lineGross : 0m;

        var tenantId = RequireTenant();
        var categoryNames = (await categoryRepository.ListByTenantAsync(tenantId, cancellationToken))
            .ToDictionary(c => c.Id, c => c.Name);
        var categories = categoryTotals
            .Where(c => !c.IsOpenItem)
            .Select(c => Group(
                c.CategoryId,
                c.CategoryId is { } id && categoryNames.TryGetValue(id, out var name) ? name : "Uncategorised",
                c.Quantity, c.Revenue, c.KnownGross, c.Cost, factor))
            .OrderByDescending(g => g.KnownCostRevenue)
            .ToList();

        var ranked = itemTotals.Where(i => i.KnownGross > 0m)
            .Select(i => (Item: i, Profit: (i.KnownGross * factor) - i.Cost))
            .ToList();
        var top = ranked.OrderByDescending(r => r.Profit).Take(RankedItems).ToList();
        var bottom = ranked.OrderBy(r => r.Profit).Where(r => !top.Contains(r)).Take(RankedItems).ToList();
        var itemNames = (await itemRepository.ListByIdsAsync([.. top.Concat(bottom).Select(r => r.Item.ItemId)], cancellationToken))
            .ToDictionary(i => i.Id, i => i.Name);
        ProfitGroupDto ToGroup((ItemTotals Item, decimal Profit) r) => Group(
            r.Item.ItemId,
            itemNames.GetValueOrDefault(r.Item.ItemId, "(removed item)"),
            r.Item.Quantity, r.Item.LineGross, r.Item.KnownGross, r.Item.Cost, factor);

        return new ProfitReportDto(
            revenueExVat,
            knownRevenue,
            revenueExVat - knownRevenue,
            cost,
            knownRevenue - cost,
            Pct(knownRevenue - cost, knownRevenue),
            Pct(knownGross, lineGross),
            days,
            categories,
            [.. top.Select(ToGroup)],
            [.. bottom.Select(ToGroup)]);
    }

    public async Task<DiscountReportDto> GetDiscountsAsync(Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default)
    {
        var scopedBranchId = await ScopeAsync(branchId, fromUtc, toUtc, cancellationToken);
        var staff = await repository.GetStaffDiscountTotalsAsync(scopedBranchId, fromUtc, toUtc, cancellationToken);
        var approvers = await repository.GetApproverCountsAsync(fromUtc, toUtc, cancellationToken);
        var names = await UserNamesAsync(cancellationToken);

        // Same split as the X/Z reading: the cart discount is Senior/PWD (on Senior/PWD sales) plus the promo code,
        // and whatever remains is manual; item promos are stored apart.
        static decimal Manual(StaffDiscountTotals t) => t.DiscountAmount - t.PromoCodeDiscount - t.SeniorPwdDiscount;
        var promoCode = staff.Sum(t => t.PromoCodeDiscount);
        var itemPromo = staff.Sum(t => t.ItemPromoDiscount);
        var senior = staff.Sum(t => t.SeniorPwdDiscount);
        var manual = staff.Sum(Manual);
        var totalDiscounts = promoCode + itemPromo + senior + manual;
        var sales = staff.Sum(t => t.Total);

        return new DiscountReportDto(
            sales + totalDiscounts,
            promoCode,
            itemPromo,
            manual,
            senior,
            totalDiscounts,
            Pct(totalDiscounts, sales + totalDiscounts) ?? 0m,
            [.. staff
                .Select(t =>
                {
                    var discounts = t.PromoCodeDiscount + t.ItemPromoDiscount + t.SeniorPwdDiscount + Manual(t);
                    return new DiscountByStaffDto(
                        t.StaffUserId,
                        t.StaffUserId is { } id ? names.GetValueOrDefault(id, "(removed user)") : "(unknown)",
                        t.SaleCount, t.Total, discounts, Pct(discounts, t.Total + discounts) ?? 0m);
                })
                .OrderByDescending(r => r.Discounts)],
            [.. approvers
                .GroupBy(a => a.ApproverUserId)
                .Select(g => new DiscountApproverDto(
                    g.Key,
                    names.GetValueOrDefault(g.Key, "(removed user)"),
                    g.Where(a => a.ActionType == AuditActionType.DiscountOverride).Sum(a => a.Count),
                    g.Where(a => a.ActionType == AuditActionType.PriceOverride).Sum(a => a.Count)))
                .OrderByDescending(a => a.DiscountApprovals + a.PriceOverrideApprovals)]);
    }

    public async Task<PageDto<ManualDiscountRowDto>> ListManualDiscountsAsync(
        Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, int offset, int limit, CancellationToken cancellationToken = default)
    {
        var scopedBranchId = await ScopeAsync(branchId, fromUtc, toUtc, cancellationToken);
        var (clampedOffset, clampedLimit) = Clamp(offset, limit);
        var page = await repository.ListManualDiscountsAsync(scopedBranchId, fromUtc, toUtc, clampedOffset, clampedLimit, cancellationToken);
        var branches = await BranchNamesAsync(cancellationToken);
        var users = await UserNamesAsync(cancellationToken);

        return new PageDto<ManualDiscountRowDto>(
            [.. page.Rows.Select(r => new ManualDiscountRowDto(
                r.TransactionId, r.ReceiptNumber, r.CompletedAt,
                branches.GetValueOrDefault(r.BranchId, "(deleted branch)"),
                Name(users, r.StaffUserId),
                r.Total, r.CartDiscount, r.CartKind, r.CartValue, r.LineDiscounts, r.LineDiscountCount, r.PriceOverrideCount,
                r.CartReason,
                r.ApprovedByUserId is { } id ? users.GetValueOrDefault(id, "(removed user)") : null))],
            page.Total, clampedOffset, clampedLimit);
    }

    public async Task<VoidReportDto> GetVoidsAsync(
        Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, int offset, int limit, CancellationToken cancellationToken = default)
    {
        var scopedBranchId = await ScopeAsync(branchId, fromUtc, toUtc, cancellationToken);
        var (clampedOffset, clampedLimit) = Clamp(offset, limit);
        var page = await repository.ListVoidsAsync(scopedBranchId, fromUtc, toUtc, clampedOffset, clampedLimit, cancellationToken);
        var hourly = await repository.GetHourlyVoidsAsync(scopedBranchId, fromUtc, toUtc, cancellationToken);
        var exclusions = await repository.GetVoidExclusionsAsync(scopedBranchId, fromUtc, toUtc, cancellationToken);
        var branches = await BranchNamesAsync(cancellationToken);
        var users = await UserNamesAsync(cancellationToken);

        return new VoidReportDto(
            hourly.Sum(h => h.Count),
            hourly.Sum(h => h.Amount),
            exclusions.EmptyDiscarded,
            exclusions.ExpiredParked,
            new PageDto<VoidRowDto>(
                [.. page.Rows.Select(r => new VoidRowDto(
                    r.TransactionId, r.VoidedAt,
                    branches.GetValueOrDefault(r.BranchId, "(deleted branch)"),
                    Name(users, r.StaffUserId ?? r.ActorUserId),
                    r.Total, r.LineCount, r.ParkedLabel,
                    JsonReason(r.AfterStateJson),
                    r.ApprovedByUserId is { } id ? users.GetValueOrDefault(id, "(removed user)") : null))],
                page.Total, clampedOffset, clampedLimit));
    }

    public async Task<RefundReportDto> GetRefundsAsync(
        Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, int offset, int limit, CancellationToken cancellationToken = default)
    {
        var scopedBranchId = await ScopeAsync(branchId, fromUtc, toUtc, cancellationToken);
        var (clampedOffset, clampedLimit) = Clamp(offset, limit);
        var refunds = await repository.ListRefundsAsync(scopedBranchId, fromUtc, toUtc, clampedOffset, clampedLimit, cancellationToken);
        var exchanges = await repository.ListExchangesAsync(scopedBranchId, fromUtc, toUtc, 0, MaxPageSize, cancellationToken);
        var hourly = await repository.GetHourlyRefundsAsync(scopedBranchId, fromUtc, toUtc, cancellationToken);
        var exchangeTotal = await repository.GetExchangePriceDifferenceAsync(scopedBranchId, fromUtc, toUtc, cancellationToken);
        var branches = await BranchNamesAsync(cancellationToken);
        var users = await UserNamesAsync(cancellationToken);

        return new RefundReportDto(
            hourly.Sum(h => h.Count),
            hourly.Sum(h => h.Amount),
            exchangeTotal,
            new PageDto<RefundRowDto>(
                [.. refunds.Rows.Select(r => new RefundRowDto(
                    r.TransactionId, r.ReceiptNumber, r.CompletedAt, r.RefundedAt,
                    branches.GetValueOrDefault(r.BranchId, "(deleted branch)"),
                    Name(users, r.StaffUserId),
                    r.Total, JsonReason(r.AfterStateJson),
                    r.ApprovedByUserId is { } id ? users.GetValueOrDefault(id, "(removed user)") : null))],
                refunds.Total, clampedOffset, clampedLimit),
            new PageDto<ExchangeRowDto>(
                [.. exchanges.Rows.Select(e => new ExchangeRowDto(
                    e.AdjustmentId, e.OriginalTransactionId, e.OriginalReceiptNumber, e.CreatedAt,
                    branches.GetValueOrDefault(e.BranchId, "(deleted branch)"),
                    e.ReturnedTotal, e.ReplacementTotal, e.PriceDifference, e.SettlementMethod, e.Reason,
                    users.GetValueOrDefault(e.ApprovedByUserId, "(removed user)")))],
                exchanges.Total, 0, MaxPageSize));
    }

    public async Task<DashboardInsightsDto> GetInsightsAsync(Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default)
    {
        var scopedBranchId = await ScopeAsync(branchId, fromUtc, toUtc, cancellationToken);
        var saleFacts = await repository.GetHourlySaleFactsAsync(scopedBranchId, fromUtc, toUtc, cancellationToken);
        var lineFacts = await repository.GetHourlyLineFactsAsync(scopedBranchId, fromUtc, toUtc, cancellationToken);
        var voids = await repository.GetHourlyVoidsAsync(scopedBranchId, fromUtc, toUtc, cancellationToken);
        var refunds = await repository.GetHourlyRefundsAsync(scopedBranchId, fromUtc, toUtc, cancellationToken);
        var categoryTotals = await repository.GetCategoryTotalsAsync(scopedBranchId, fromUtc, toUtc, cancellationToken);

        var marginDays = ProfitDays(saleFacts, lineFacts);
        var revenueExVat = saleFacts.Sum(RevenueExVat);
        var lineGross = lineFacts.Sum(f => f.LineGross);
        var knownGross = lineFacts.Sum(f => f.KnownGross);
        var knownRevenue = Allocate(revenueExVat, knownGross, lineGross);
        var profit = knownRevenue - lineFacts.Sum(f => f.Cost);

        var voidByDay = LocalDays(voids, v => (v.UtcDate, v.UtcHour)).ToDictionary(g => g.Key, g => g.Sum(v => v.Amount));
        var refundByDay = LocalDays(refunds, v => (v.UtcDate, v.UtcHour)).ToDictionary(g => g.Key, g => g.Sum(v => v.Amount));
        var saleDays = LocalDays(saleFacts, f => (f.UtcDate, f.UtcHour)).ToDictionary(g => g.Key, g => g.ToList());
        var leakage = saleDays.Keys.Union(voidByDay.Keys).Union(refundByDay.Keys).OrderBy(d => d)
            .Select(date =>
            {
                var sales = saleDays.GetValueOrDefault(date) ?? [];
                var discounts = sales.Sum(f => f.DiscountAmount + f.ItemPromoDiscount);
                var gross = sales.Sum(f => f.Total) + discounts;
                var voided = voidByDay.GetValueOrDefault(date);
                var refunded = refundByDay.GetValueOrDefault(date);
                return new LeakageDayDto(date, gross, discounts, voided, refunded, Pct(discounts + voided + refunded, gross) ?? 0m);
            })
            .ToList();
        var leakageGross = leakage.Sum(d => d.GrossSales);

        var factor = lineGross > 0m ? revenueExVat / lineGross : 0m;
        var categoryNames = (await categoryRepository.ListByTenantAsync(RequireTenant(), cancellationToken))
            .ToDictionary(c => c.Id, c => c.Name);
        var totalRevenue = categoryTotals.Sum(c => c.Revenue);
        var categories = categoryTotals
            .OrderByDescending(c => c.Revenue)
            .Take(DashboardCategories)
            .Select(c => new CategoryShareDto(
                c.IsOpenItem ? "Open items"
                    : c.CategoryId is { } id && categoryNames.TryGetValue(id, out var name) ? name : "Uncategorised",
                c.IsOpenItem,
                c.Revenue,
                Pct(c.Revenue, totalRevenue) ?? 0m,
                Pct((c.KnownGross * factor) - c.Cost, c.KnownGross * factor)))
            .ToList();

        return new DashboardInsightsDto(
            marginDays,
            Pct(profit, knownRevenue),
            Pct(knownGross, lineGross),
            leakage,
            Pct(leakage.Sum(d => d.Discounts + d.Voids + d.Refunds), leakageGross) ?? 0m,
            categories);
    }

    public async Task<string> ExportVoidsCsvAsync(Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default)
    {
        var report = await GetVoidsAsync(branchId, fromUtc, toUtc, 0, IReportingRepository.MaxExportRows + 1, cancellationToken);
        RequireWithinExportCap(report.Voids.Total);
        return ReportCsv.Build(
            "Voided At,Branch,Staff,Total,Lines,Label,Reason,Approver",
            report.Voids.Rows.Select(r => new[]
            {
                ReportCsv.Time(r.VoidedAt), ReportCsv.Field(r.BranchName), ReportCsv.Field(r.StaffName), ReportCsv.Money(r.Total),
                r.LineCount.ToString(System.Globalization.CultureInfo.InvariantCulture), ReportCsv.Field(r.ParkedLabel ?? ""),
                ReportCsv.Field(r.Reason ?? ""), ReportCsv.Field(r.ApproverName ?? ""),
            }));
    }

    public async Task<string> ExportRefundsCsvAsync(Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default)
    {
        var report = await GetRefundsAsync(branchId, fromUtc, toUtc, 0, IReportingRepository.MaxExportRows + 1, cancellationToken);
        RequireWithinExportCap(report.Refunds.Total);
        return ReportCsv.Build(
            "Refunded At,Receipt Number,Sold At,Branch,Staff,Total,Reason,Approver",
            report.Refunds.Rows.Select(r => new[]
            {
                ReportCsv.Time(r.RefundedAt), r.ReceiptNumber?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
                ReportCsv.Time(r.CompletedAt), ReportCsv.Field(r.BranchName), ReportCsv.Field(r.StaffName), ReportCsv.Money(r.Total),
                ReportCsv.Field(r.Reason ?? ""), ReportCsv.Field(r.ApproverName ?? ""),
            }));
    }

    public async Task<string> ExportManualDiscountsCsvAsync(Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default)
    {
        var page = await ListManualDiscountsAsync(branchId, fromUtc, toUtc, 0, IReportingRepository.MaxExportRows + 1, cancellationToken);
        RequireWithinExportCap(page.Total);
        return ReportCsv.Build(
            "Sold At,Receipt Number,Branch,Staff,Total,Cart Discount,Line Discounts,Lines Discounted,Price Overrides,Reason,Approver",
            page.Rows.Select(r => new[]
            {
                ReportCsv.Time(r.CompletedAt), r.ReceiptNumber?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
                ReportCsv.Field(r.BranchName), ReportCsv.Field(r.StaffName), ReportCsv.Money(r.Total),
                ReportCsv.Money(r.CartDiscount), ReportCsv.Money(r.LineDiscounts),
                r.LineDiscountCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                r.PriceOverrideCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ReportCsv.Field(r.Reason ?? ""), ReportCsv.Field(r.ApproverName ?? ""),
            }));
    }

    private static void RequireWithinExportCap(int total)
    {
        if (total > IReportingRepository.MaxExportRows)
        {
            throw new ValidationException(
                "range",
                $"That period has more than {IReportingRepository.MaxExportRows:N0} rows. Choose a shorter date range and export it in parts.");
        }
    }

    private async Task<Guid?> ScopeAsync(Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken)
    {
        ReportRange.Validate(fromUtc, toUtc);
        return await reportScopeResolver.ResolveBranchIdAsync(branchId, cancellationToken);
    }

    private static (int Offset, int Limit) Clamp(int offset, int limit) =>
        (Math.Max(0, offset), limit > IReportingRepository.MaxExportRows ? IReportingRepository.MaxExportRows + 1 : Math.Clamp(limit, 1, MaxPageSize));

    /// <summary>Groups hour-bucketed facts by UTC+8 business day. The offset is whole hours, so the shift is exact.</summary>
    private static IEnumerable<IGrouping<DateOnly, T>> LocalDays<T>(IEnumerable<T> facts, Func<T, (DateOnly UtcDate, int UtcHour)> key) =>
        facts.GroupBy(f =>
        {
            var (utcDate, utcHour) = key(f);
            return DateOnly.FromDateTime(new DateTime(utcDate.Year, utcDate.Month, utcDate.Day, utcHour, 0, 0).Add(ReportTimeZone.Offset));
        }).OrderBy(g => g.Key);

    private static decimal RevenueExVat(HourlySaleFacts f) => f.VatableSales + f.VatExemptSales + f.ZeroRatedSales;

    /// <summary>Revenue attributable to the lines with a known cost: the sale-level net revenue spread by line total.</summary>
    private static decimal Allocate(decimal revenueExVat, decimal knownGross, decimal lineGross) =>
        lineGross > 0m ? Math.Round(revenueExVat * knownGross / lineGross, 2) : 0m;

    private static decimal? Pct(decimal part, decimal whole) => whole > 0m ? Math.Round(part / whole * 100m, 1) : null;

    private static ProfitGroupDto Group(Guid? id, string name, decimal quantity, decimal lineGross, decimal knownGross, decimal cost, decimal factor)
    {
        var knownRevenue = Math.Round(knownGross * factor, 2);
        return new ProfitGroupDto(id, name, quantity, knownRevenue, cost, knownRevenue - cost, Pct(knownRevenue - cost, knownRevenue), Pct(knownGross, lineGross));
    }

    private static List<ProfitDayDto> ProfitDays(IReadOnlyList<HourlySaleFacts> saleFacts, IReadOnlyList<HourlyLineFacts> lineFacts)
    {
        var sales = LocalDays(saleFacts, f => (f.UtcDate, f.UtcHour)).ToDictionary(g => g.Key, g => g.Sum(RevenueExVat));
        var lines = LocalDays(lineFacts, f => (f.UtcDate, f.UtcHour)).ToDictionary(g => g.Key, g => g.ToList());

        return [.. sales.Keys.OrderBy(d => d).Select(date =>
        {
            var dayLines = lines.GetValueOrDefault(date) ?? [];
            var lineGross = dayLines.Sum(l => l.LineGross);
            var knownGross = dayLines.Sum(l => l.KnownGross);
            var cost = dayLines.Sum(l => l.Cost);
            var knownRevenue = Allocate(sales[date], knownGross, lineGross);
            return new ProfitDayDto(date, knownRevenue, cost, knownRevenue - cost, Pct(knownRevenue - cost, knownRevenue), Pct(knownGross, lineGross));
        })];
    }

    private static string? JsonReason(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty("reason", out var reason) && reason.ValueKind == JsonValueKind.String
                ? reason.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Name(Dictionary<Guid, string> users, Guid? userId) =>
        userId is { } id ? users.GetValueOrDefault(id, "(removed user)") : "(unknown)";

    private async Task<Dictionary<Guid, string>> UserNamesAsync(CancellationToken cancellationToken) =>
        (await userRepository.ListActorsAsync(RequireTenant(), cancellationToken)).ToDictionary(u => u.Id, u => u.Name);

    private async Task<Dictionary<Guid, string>> BranchNamesAsync(CancellationToken cancellationToken) =>
        (await branchRepository.ListByTenantAsync(RequireTenant(), cancellationToken)).ToDictionary(b => b.Id, b => b.Name);

    private Guid RequireTenant() => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("Reports require an authenticated tenant context.");
}
