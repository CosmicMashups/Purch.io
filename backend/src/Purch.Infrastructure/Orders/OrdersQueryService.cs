using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Purch.Application.Common.Exceptions;
using Purch.Application.Orders;
using Purch.Application.Reporting;
using Purch.Domain.Entities;
using Purch.Domain.Enums;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Orders;

/// <summary>The back-office sales list: every finished sale of the business (or of the caller's own branch, for a
/// branch-scoped Manager), filterable, with enough on each row to find a receipt without knowing its number.</summary>
public sealed class OrdersQueryService(PurchDbContext db, IReportScopeResolver scopeResolver) : IOrdersQueryService
{
    private const int MaxExportRows = 20000;
    private const int MaxPageSize = 100;

    public async Task<OrdersPageDto> ListAsync(OrdersQuery query, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
        var filtered = await FilterAsync(query, cancellationToken);
        var total = await filtered.CountAsync(cancellationToken);
        var rows = await filtered
            .OrderByDescending(t => t.CompletedAt ?? t.CreatedAt)
            .ThenByDescending(t => t.ReceiptNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new OrdersPageDto(await ToItemsAsync(rows, cancellationToken), total, page, pageSize);
    }

    public async Task<string> ExportCsvAsync(OrdersQuery query, CancellationToken cancellationToken = default)
    {
        var filtered = await FilterAsync(query, cancellationToken);
        var rows = await filtered.OrderByDescending(t => t.CompletedAt ?? t.CreatedAt).Take(MaxExportRows).ToListAsync(cancellationToken);
        var items = await ToItemsAsync(rows, cancellationToken);

        var csv = new StringBuilder();
        _ = csv.AppendLine("Receipt,Date,Status,Branch,Device,Cashier,Customer,Payment,Total");
        foreach (var item in items)
        {
            _ = csv.AppendJoin(
                ',',
                Cell(item.ReceiptNumber?.ToString(CultureInfo.InvariantCulture)),
                Cell(item.At.ToString("yyyy-MM-dd HH:mm:ss'Z'", CultureInfo.InvariantCulture)),
                Cell(item.HasExchange ? item.Status + " (exchanged)" : item.Status),
                Cell(item.BranchName),
                Cell(item.DeviceName),
                Cell(item.StaffName),
                Cell(item.CustomerName),
                Cell(string.Join(" + ", item.PaymentMethods)),
                Cell(item.TotalAmount.ToString("0.00", CultureInfo.InvariantCulture)));
            _ = csv.AppendLine();
        }

        return csv.ToString();
    }

    public async Task EnsureVisibleAsync(Guid transactionId, CancellationToken cancellationToken = default)
    {
        var confinedTo = await scopeResolver.ResolveBranchIdAsync(null, cancellationToken);
        var visible = await db.Transactions.AnyAsync(
            t => t.Id == transactionId && t.Status != TransactionStatus.Open && t.Status != TransactionStatus.AwaitingPayment && (confinedTo == null || t.BranchId == confinedTo),
            cancellationToken);
        if (!visible)
        {
            throw new NotFoundException("Order", transactionId);
        }
    }

    private async Task<IQueryable<Transaction>> FilterAsync(OrdersQuery query, CancellationToken cancellationToken)
    {
        // A branch-scoped Manager is held to their own branch whatever the request asks for.
        var branchId = await scopeResolver.ResolveBranchIdAsync(query.BranchId, cancellationToken);

        // Carts still being rung up aren't orders yet, and a kiosk order waiting for a cashier has no receipt.
        var q = db.Transactions.AsNoTracking()
            .Where(t => t.Status == TransactionStatus.Completed || t.Status == TransactionStatus.Voided || t.Status == TransactionStatus.Refunded);

        if (branchId is { } b)
        {
            q = q.Where(t => t.BranchId == b);
        }

        if (query.FromUtc is { } from)
        {
            q = q.Where(t => (t.CompletedAt ?? t.CreatedAt) >= from);
        }

        if (query.ToExclusiveUtc is { } to)
        {
            q = q.Where(t => (t.CompletedAt ?? t.CreatedAt) < to);
        }

        if (query.DeviceId is { } deviceId)
        {
            q = q.Where(t => t.DeviceId == deviceId);
        }

        if (query.StaffUserId is { } staffId)
        {
            q = q.Where(t => t.StaffUserId == staffId);
        }

        if (query.Method is { } method)
        {
            q = q.Where(t => db.Payments.Any(p => p.TransactionId == t.Id && p.Method == method));
        }

        q = query.Status switch
        {
            OrderStatusFilter.Completed => q.Where(t => t.Status == TransactionStatus.Completed),
            OrderStatusFilter.Voided => q.Where(t => t.Status == TransactionStatus.Voided),
            OrderStatusFilter.Refunded => q.Where(t => t.Status == TransactionStatus.Refunded),
            OrderStatusFilter.Exchanged => q.Where(t => db.Adjustments.Any(a => a.OriginalTransactionId == t.Id)),
            _ => q,
        };

        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            if (long.TryParse(search.TrimStart('#'), NumberStyles.None, CultureInfo.InvariantCulture, out var receipt))
            {
                q = q.Where(t => t.ReceiptNumber == receipt);
            }
            else
            {
                var pattern = "%" + search.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
                q = q.Where(t => db.CreditTransactions.Any(c => c.TransactionId == t.Id
                    && db.CustomerCreditLedgers.Any(l => l.Id == c.CustomerCreditLedgerId && EF.Functions.ILike(l.CustomerFullName, pattern, "\\"))));
            }
        }

        return q;
    }

    private async Task<IReadOnlyList<OrderListItemDto>> ToItemsAsync(List<Transaction> rows, CancellationToken ct)
    {
        var ids = rows.Select(r => r.Id).ToList();
        var branches = await db.Branches.AsNoTracking().ToDictionaryAsync(b => b.Id, b => b.Name, ct);
        var devices = await db.Devices.AsNoTracking().ToDictionaryAsync(d => d.Id, d => d.Name ?? d.DeviceType.ToString(), ct);
        var staffIds = rows.Where(r => r.StaffUserId != null).Select(r => r.StaffUserId!.Value).Distinct().ToList();
        var staff = new Dictionary<Guid, string>();
        foreach (var m in await db.Memberships.AsNoTracking().Include(x => x.Account).Where(m => staffIds.Contains(m.Id)).ToListAsync(ct))
        {
            staff[m.Id] = m.Account?.DisplayName ?? string.Empty;
        }

        foreach (var u in await db.Users.AsNoTracking().Where(u => staffIds.Contains(u.Id)).ToListAsync(ct))
        {
            _ = staff.TryAdd(u.Id, u.Name);
        }

        var methods = (await db.Payments.AsNoTracking().Where(p => ids.Contains(p.TransactionId)).Select(p => new { p.TransactionId, p.Method }).ToListAsync(ct))
            .GroupBy(p => p.TransactionId).ToDictionary(g => g.Key, g => (IReadOnlyList<string>)[.. g.Select(p => p.Method.ToString()).Distinct()]);
        var exchanged = (await db.Adjustments.AsNoTracking().Where(a => ids.Contains(a.OriginalTransactionId)).Select(a => a.OriginalTransactionId).ToListAsync(ct)).ToHashSet();
        var customers = (await (
            from c in db.CreditTransactions.AsNoTracking()
            join l in db.CustomerCreditLedgers.AsNoTracking() on c.CustomerCreditLedgerId equals l.Id
            where c.TransactionId != null && ids.Contains(c.TransactionId.Value)
            select new { TransactionId = c.TransactionId!.Value, l.CustomerFullName }).ToListAsync(ct))
            .GroupBy(x => x.TransactionId).ToDictionary(g => g.Key, g => g.First().CustomerFullName);

        return [.. rows.Select(r => new OrderListItemDto(
            r.Id,
            r.ReceiptNumber,
            r.CompletedAt ?? r.CreatedAt,
            r.Status.ToString(),
            exchanged.Contains(r.Id),
            r.BranchId,
            branches.GetValueOrDefault(r.BranchId, string.Empty),
            r.DeviceId,
            devices.GetValueOrDefault(r.DeviceId, string.Empty),
            r.StaffUserId is { } s ? staff.GetValueOrDefault(s) : null,
            customers.GetValueOrDefault(r.Id),
            methods.GetValueOrDefault(r.Id, []),
            r.TotalAmount))];
    }

    // Quote every cell, and defuse a leading = + - @ so a customer name can't run as a formula when opened in Excel.
    private static string Cell(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Length > 0 && text[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            text = "'" + text;
        }

        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }
}
