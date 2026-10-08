using Purch.Application.Common.Exceptions;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.Pos;

/// <summary>The rules every refund and exchange follows beyond a manager's PIN: it happens in the branch the sale was made in,
/// within a set number of days, and a returned item is worth what the customer actually paid for it. An owner-level Admin
/// (not confined to a branch) may go past the first two. These are business rules, not permissions, so they refuse with a
/// validation error whose message the apps show as written (a 403 would only read "you do not have permission").</summary>
public static class ReturnPolicy
{
    public const int WindowDays = 30;

    /// <summary>An Admin whose account is not confined to a branch or department.</summary>
    public static bool IsTenantWideAdmin(Role? role, ScopeType? scopeType) =>
        role == Role.Admin && scopeType is null or ScopeType.Tenant;

    public static void EnsureAllowed(Transaction sale, Guid? actorBranchId, bool tenantWideAdmin, DateTimeOffset now)
    {
        if (tenantWideAdmin)
        {
            return;
        }

        if (actorBranchId is { } branchId && branchId != sale.BranchId)
        {
            throw new ValidationException(nameof(sale.BranchId), "This sale was made at another branch, so it can only be returned there or by an owner-level admin.");
        }

        var soldAt = sale.CompletedAt ?? sale.CreatedAt;
        if (soldAt < now.AddDays(-WindowDays))
        {
            throw new ValidationException(nameof(sale.CompletedAt), $"This sale is more than {WindowDays} days old, so only an owner-level admin can return it.");
        }
    }

    /// <summary>What the customer paid for <paramref name="quantity"/> of <paramref name="line"/>. The line's own promotion
    /// (buy-1-take-1, combo, item discount) comes off that line; everything charged to the whole sale (Senior/PWD VAT exemption
    /// and 20%, a promo code) is shared across the lines in proportion to what they cost after their own promotion. So the paid
    /// values of all lines add up to the sale's total.</summary>
    public static decimal PaidValue(Transaction sale, IReadOnlyCollection<TransactionLine> lines, TransactionLine line, decimal quantity)
    {
        var netOfLinePromos = lines.Sum(l => l.LineTotal - l.PromoDiscountAmount);
        if (netOfLinePromos <= 0 || line.Quantity <= 0)
        {
            return 0m;
        }

        var paidForLine = (line.LineTotal - line.PromoDiscountAmount) * sale.TotalAmount / netOfLinePromos;
        return Math.Round(paidForLine * quantity / line.Quantity, 2, MidpointRounding.AwayFromZero);
    }
}
