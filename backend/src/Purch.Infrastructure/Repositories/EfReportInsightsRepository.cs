using Microsoft.EntityFrameworkCore;
using Purch.Application.Reporting;
using Purch.Domain.Entities;
using Purch.Domain.Enums;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfReportInsightsRepository(PurchDbContext dbContext) : IReportInsightsRepository
{
    public async Task<IReadOnlyList<PaymentTotals>> GetPaymentTotalsAsync(
        Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default)
    {
        var rows = await (
                from payment in dbContext.Payments.AsNoTracking()
                join transaction in Sales(branchId, fromUtc, toUtc) on payment.TransactionId equals transaction.Id
                where payment.Status == PaymentStatus.Confirmed
                group payment by payment.Method into byMethod
                select new { Method = byMethod.Key, Amount = byMethod.Sum(p => p.Amount), Count = byMethod.Count() })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(r => new PaymentTotals(r.Method, r.Amount, r.Count))];
    }

    public async Task<IReadOnlyList<CategoryTotals>> GetCategoryTotalsAsync(
        Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default)
    {
        var rows = await (
                from line in dbContext.TransactionLines.AsNoTracking()
                join transaction in Sales(branchId, fromUtc, toUtc) on line.TransactionId equals transaction.Id
                join item in dbContext.Items.AsNoTracking() on line.ItemId equals item.Id into itemJoin
                from item in itemJoin.DefaultIfEmpty()
                group line by new { CategoryId = (Guid?)item.CategoryId, line.IsOpenItem } into byCategory
                select new
                {
                    byCategory.Key.CategoryId,
                    byCategory.Key.IsOpenItem,
                    Revenue = byCategory.Sum(l => l.LineTotal),
                    Quantity = byCategory.Sum(l => l.Quantity),
                    KnownGross = byCategory.Sum(l => l.UnitCost != null ? l.LineTotal : 0m),
                    Cost = byCategory.Sum(l => l.UnitCost != null ? l.Quantity * l.UnitCost.Value : 0m),
                })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(r => new CategoryTotals(r.CategoryId, r.IsOpenItem, r.Revenue, r.Quantity, r.KnownGross, r.Cost))];
    }

    public async Task<IReadOnlyList<ItemTotals>> GetItemTotalsAsync(
        Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default)
    {
        var rows = await (
                from line in dbContext.TransactionLines.AsNoTracking()
                join transaction in Sales(branchId, fromUtc, toUtc) on line.TransactionId equals transaction.Id
                where line.ItemId != null
                group line by line.ItemId into byItem
                select new
                {
                    ItemId = byItem.Key,
                    Quantity = byItem.Sum(l => l.Quantity),
                    LineGross = byItem.Sum(l => l.LineTotal),
                    KnownGross = byItem.Sum(l => l.UnitCost != null ? l.LineTotal : 0m),
                    Cost = byItem.Sum(l => l.UnitCost != null ? l.Quantity * l.UnitCost.Value : 0m),
                })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(r => new ItemTotals(r.ItemId!.Value, r.Quantity, r.LineGross, r.KnownGross, r.Cost))];
    }

    public async Task<IReadOnlyList<HourlySaleFacts>> GetHourlySaleFactsAsync(
        Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default)
    {
        // Project the instant first: grouping on the coalesced expression directly does not translate, and
        // grouping on the integer parts avoids Npgsql mapping a DateTime key as a DateTimeOffset.
        var rows = await Sales(branchId, fromUtc, toUtc)
            .Select(t => new
            {
                Instant = t.CompletedAt ?? t.CreatedAt,
                t.TotalAmount,
                t.VatableSales,
                t.VatAmount,
                t.VatExemptSales,
                t.ZeroRatedSales,
                t.VatDerived,
                t.SeniorPwdDiscountApplied,
                t.VatExemptAmount,
                t.DiscountAmount,
                t.PromoDiscountAmount,
                t.ItemPromoDiscountAmount,
            })
            .GroupBy(t => new
            {
                t.Instant.UtcDateTime.Year,
                t.Instant.UtcDateTime.Month,
                t.Instant.UtcDateTime.Day,
                t.Instant.UtcDateTime.Hour,
            })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                g.Key.Day,
                g.Key.Hour,
                Count = g.Count(),
                Total = g.Sum(t => t.TotalAmount),
                Vatable = g.Sum(t => t.VatableSales),
                Vat = g.Sum(t => t.VatAmount),
                Exempt = g.Sum(t => t.VatExemptSales),
                Zero = g.Sum(t => t.ZeroRatedSales),
                Derived = g.Count(t => t.VatDerived),
                SeniorCount = g.Count(t => t.SeniorPwdDiscountApplied),
                SeniorExempt = g.Sum(t => t.SeniorPwdDiscountApplied ? t.VatExemptAmount : 0m),
                Discount = g.Sum(t => t.DiscountAmount),
                PromoCode = g.Sum(t => t.PromoDiscountAmount),
                ItemPromo = g.Sum(t => t.ItemPromoDiscountAmount),
                Senior = g.Sum(t => t.SeniorPwdDiscountApplied ? t.DiscountAmount - t.PromoDiscountAmount : 0m),
            })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(r => new HourlySaleFacts(
            new DateOnly(r.Year, r.Month, r.Day), r.Hour, r.Count, r.Total,
            r.Vatable, r.Vat, r.Exempt, r.Zero, r.Derived, r.SeniorCount, r.SeniorExempt,
            r.Discount, r.PromoCode, r.ItemPromo, r.Senior))];
    }

    public async Task<IReadOnlyList<HourlyLineFacts>> GetHourlyLineFactsAsync(
        Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default)
    {
        var rows = await (
                from line in dbContext.TransactionLines.AsNoTracking()
                join transaction in Sales(branchId, fromUtc, toUtc) on line.TransactionId equals transaction.Id
                select new
                {
                    Instant = transaction.CompletedAt ?? transaction.CreatedAt,
                    line.LineTotal,
                    line.Quantity,
                    line.UnitCost,
                })
            .GroupBy(l => new
            {
                l.Instant.UtcDateTime.Year,
                l.Instant.UtcDateTime.Month,
                l.Instant.UtcDateTime.Day,
                l.Instant.UtcDateTime.Hour,
            })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                g.Key.Day,
                g.Key.Hour,
                LineGross = g.Sum(l => l.LineTotal),
                KnownGross = g.Sum(l => l.UnitCost != null ? l.LineTotal : 0m),
                Cost = g.Sum(l => l.UnitCost != null ? l.Quantity * l.UnitCost.Value : 0m),
            })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(r => new HourlyLineFacts(new DateOnly(r.Year, r.Month, r.Day), r.Hour, r.LineGross, r.KnownGross, r.Cost))];
    }

    public async Task<IReadOnlyList<StaffDiscountTotals>> GetStaffDiscountTotalsAsync(
        Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default)
    {
        var rows = await Sales(branchId, fromUtc, toUtc)
            .GroupBy(t => t.StaffUserId)
            .Select(g => new
            {
                Staff = g.Key,
                Count = g.Count(),
                Total = g.Sum(t => t.TotalAmount),
                Discount = g.Sum(t => t.DiscountAmount),
                PromoCode = g.Sum(t => t.PromoDiscountAmount),
                ItemPromo = g.Sum(t => t.ItemPromoDiscountAmount),
                Senior = g.Sum(t => t.SeniorPwdDiscountApplied ? t.DiscountAmount - t.PromoDiscountAmount : 0m),
            })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(r => new StaffDiscountTotals(r.Staff, r.Count, r.Total, r.Discount, r.PromoCode, r.ItemPromo, r.Senior))];
    }

    public async Task<IReadOnlyList<ApproverCount>> GetApproverCountsAsync(
        DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.AuditLogs.AsNoTracking()
            .Where(a => a.CreatedAt >= fromUtc && a.CreatedAt < toUtc
                && a.ApprovedByUserId != null
                && (a.ActionType == AuditActionType.DiscountOverride || a.ActionType == AuditActionType.PriceOverride))
            .GroupBy(a => new { a.ApprovedByUserId, a.ActionType })
            .Select(g => new { g.Key.ApprovedByUserId, g.Key.ActionType, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(r => new ApproverCount(r.ApprovedByUserId!.Value, r.ActionType, r.Count))];
    }

    public async Task<IReadOnlyList<HourlyEvent>> GetHourlyVoidsAsync(
        Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default)
    {
        var rows = await VoidedWithAudit(branchId, fromUtc, toUtc)
            .Select(v => new { Instant = v.Audit.CreatedAt, v.Transaction.TotalAmount })
            .GroupBy(v => new
            {
                v.Instant.UtcDateTime.Year,
                v.Instant.UtcDateTime.Month,
                v.Instant.UtcDateTime.Day,
                v.Instant.UtcDateTime.Hour,
            })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, Count = g.Count(), Amount = g.Sum(v => v.TotalAmount) })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(r => new HourlyEvent(new DateOnly(r.Year, r.Month, r.Day), r.Hour, r.Count, r.Amount))];
    }

    public async Task<IReadOnlyList<HourlyEvent>> GetHourlyRefundsAsync(
        Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default)
    {
        var rows = await Refunded(branchId, fromUtc, toUtc)
            .Select(t => new { Instant = t.RefundedAt!.Value, t.TotalAmount })
            .GroupBy(t => new
            {
                t.Instant.UtcDateTime.Year,
                t.Instant.UtcDateTime.Month,
                t.Instant.UtcDateTime.Day,
                t.Instant.UtcDateTime.Hour,
            })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, Count = g.Count(), Amount = g.Sum(t => t.TotalAmount) })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(r => new HourlyEvent(new DateOnly(r.Year, r.Month, r.Day), r.Hour, r.Count, r.Amount))];
    }

    public async Task<VoidExclusions> GetVoidExclusionsAsync(
        Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Transactions.AsNoTracking()
            .Where(t => t.Status == TransactionStatus.Voided && t.UpdatedAt >= fromUtc && t.UpdatedAt < toUtc);
        if (branchId is { } id)
        {
            query = query.Where(t => t.BranchId == id);
        }

        var row = await query
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Empty = g.Count(t => t.ParkedAt == null && t.TotalAmount == 0m),
                Expired = g.Count(t => t.ParkedAt != null),
            })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null ? new VoidExclusions(0, 0) : new VoidExclusions(row.Empty, row.Expired);
    }

    public async Task<decimal> GetExchangePriceDifferenceAsync(
        Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Adjustments.AsNoTracking()
            .Where(a => a.CreatedAt >= fromUtc && a.CreatedAt < toUtc && a.PriceDifference > 0);
        if (branchId is { } id)
        {
            query = query.Where(a => a.BranchId == id);
        }

        return await query.SumAsync(a => (decimal?)a.PriceDifference, cancellationToken) ?? 0m;
    }

    public async Task<RowPage<VoidRow>> ListVoidsAsync(
        Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, int offset, int limit, CancellationToken cancellationToken = default)
    {
        var query = VoidedWithAudit(branchId, fromUtc, toUtc);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(v => v.Audit.CreatedAt)
            .Skip(offset).Take(limit)
            .Select(v => new VoidRow(
                v.Transaction.Id,
                v.Audit.CreatedAt,
                v.Transaction.BranchId,
                v.Transaction.DeviceId,
                v.Transaction.StaffUserId,
                v.Transaction.TotalAmount,
                dbContext.TransactionLines.Count(l => l.TransactionId == v.Transaction.Id),
                v.Transaction.ParkedLabel,
                v.Audit.AfterStateJson,
                v.Audit.ApprovedByUserId,
                v.Audit.ActorUserId))
            .ToListAsync(cancellationToken);

        return new RowPage<VoidRow>(rows, total);
    }

    public async Task<RowPage<RefundRow>> ListRefundsAsync(
        Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, int offset, int limit, CancellationToken cancellationToken = default)
    {
        var query = Refunded(branchId, fromUtc, toUtc);
        var total = await query.CountAsync(cancellationToken);
        var rows = await (
                from t in query.OrderByDescending(t => t.RefundedAt).Skip(offset).Take(limit)
                join a in dbContext.AuditLogs.AsNoTracking().Where(a => a.ActionType == AuditActionType.Refund)
                    on t.Id equals a.TargetEntityId into audits
                from a in audits.DefaultIfEmpty()
                orderby t.RefundedAt descending
                select new RefundRow(
                    t.Id,
                    t.ReceiptNumber,
                    t.CompletedAt ?? t.CreatedAt,
                    t.RefundedAt!.Value,
                    t.BranchId,
                    t.StaffUserId,
                    t.TotalAmount,
                    a.AfterStateJson,
                    a.ApprovedByUserId,
                    a.ActorUserId))
            .ToListAsync(cancellationToken);

        return new RowPage<RefundRow>(rows, total);
    }

    public async Task<RowPage<ExchangeRow>> ListExchangesAsync(
        Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, int offset, int limit, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Adjustments.AsNoTracking().Where(a => a.CreatedAt >= fromUtc && a.CreatedAt < toUtc);
        if (branchId is { } id)
        {
            query = query.Where(a => a.BranchId == id);
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await (
                from a in query.OrderByDescending(a => a.CreatedAt).Skip(offset).Take(limit)
                join t in dbContext.Transactions.AsNoTracking() on a.OriginalTransactionId equals t.Id
                orderby a.CreatedAt descending
                select new ExchangeRow(
                    a.Id, a.OriginalTransactionId, t.ReceiptNumber, a.CreatedAt, a.BranchId,
                    a.ReturnedTotal, a.ReplacementTotal, a.PriceDifference, a.SettlementMethod, a.Reason, a.ApprovedByUserId))
            .ToListAsync(cancellationToken);

        return new RowPage<ExchangeRow>(rows, total);
    }

    public async Task<RowPage<ManualDiscountRow>> ListManualDiscountsAsync(
        Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc, int offset, int limit, CancellationToken cancellationToken = default)
    {
        var query = Sales(branchId, fromUtc, toUtc)
            .Where(t => t.ManualDiscountAmount > 0m
                || dbContext.TransactionLines.Any(l => l.TransactionId == t.Id && (l.ManualDiscountAmount > 0m || l.PriceOverrideFrom != null)));
        var total = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(t => t.CompletedAt ?? t.CreatedAt)
            .Skip(offset).Take(limit)
            .Select(t => new ManualDiscountRow(
                t.Id,
                t.ReceiptNumber,
                t.CompletedAt ?? t.CreatedAt,
                t.BranchId,
                t.StaffUserId,
                t.TotalAmount,
                t.ManualDiscountAmount,
                t.ManualDiscountKind,
                t.ManualDiscountValue,
                t.ManualDiscountReason,
                dbContext.TransactionLines.Where(l => l.TransactionId == t.Id).Sum(l => (decimal?)l.ManualDiscountAmount) ?? 0m,
                dbContext.TransactionLines.Count(l => l.TransactionId == t.Id && l.ManualDiscountAmount > 0m),
                dbContext.TransactionLines.Count(l => l.TransactionId == t.Id && l.PriceOverrideFrom != null),
                dbContext.AuditLogs
                    .Where(a => a.TargetEntityId == t.Id && a.ApprovedByUserId != null
                        && (a.ActionType == AuditActionType.DiscountOverride || a.ActionType == AuditActionType.PriceOverride))
                    .OrderByDescending(a => a.CreatedAt)
                    .Select(a => a.ApprovedByUserId)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);

        return new RowPage<ManualDiscountRow>(rows, total);
    }

    /// <summary>Completed and Refunded sales by completion time: gross, matching how X/Z includes a refunded sale.</summary>
    private IQueryable<Transaction> Sales(Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc)
    {
        var query = dbContext.Transactions.AsNoTracking()
            .Where(t => (t.Status == TransactionStatus.Completed || t.Status == TransactionStatus.Refunded)
                && (t.CompletedAt ?? t.CreatedAt) >= fromUtc
                && (t.CompletedAt ?? t.CreatedAt) < toUtc);
        return branchId is { } id ? query.Where(t => t.BranchId == id) : query;
    }

    private IQueryable<Transaction> Refunded(Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc)
    {
        var query = dbContext.Transactions.AsNoTracking()
            .Where(t => t.Status == TransactionStatus.Refunded && t.RefundedAt != null
                && t.RefundedAt >= fromUtc && t.RefundedAt < toUtc);
        return branchId is { } id ? query.Where(t => t.BranchId == id) : query;
    }

    /// <summary>Staff or approved voids of a cart that had value: not a parked cart the sweeper expired (it keeps
    /// ParkedAt), and not an empty discard. Dated by the void audit entry, which exists only for a cart with content.</summary>
    private IQueryable<VoidedAudit> VoidedWithAudit(Guid? branchId, DateTimeOffset fromUtc, DateTimeOffset toUtc)
    {
        var query =
            from a in dbContext.AuditLogs.AsNoTracking()
            join t in dbContext.Transactions.AsNoTracking() on a.TargetEntityId equals t.Id
            where a.ActionType == AuditActionType.Void
                && a.CreatedAt >= fromUtc && a.CreatedAt < toUtc
                && t.Status == TransactionStatus.Voided
                && t.ParkedAt == null
                && t.TotalAmount > 0m
            select new VoidedAudit { Audit = a, Transaction = t };

        return branchId is { } id ? query.Where(v => v.Transaction.BranchId == id) : query;
    }

    private sealed class VoidedAudit
    {
        public required AuditLog Audit { get; init; }

        public required Transaction Transaction { get; init; }
    }
}
