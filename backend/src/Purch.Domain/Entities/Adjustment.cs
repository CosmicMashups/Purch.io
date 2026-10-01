using Purch.Domain.Common;
using Purch.Domain.Enums;

namespace Purch.Domain.Entities;

/// <summary>
/// An exchange against an already-completed sale: some quantity of what was bought is returned, some
/// different item(s) are taken instead, and only the price difference changes hands. The original
/// Transaction is never edited — this is the linked record decision B calls for, so the receipt stays
/// exactly what was printed and every adjustment against it is its own auditable row. Always needs an
/// Admin/Manager's approval, the same as a void or a refund — see ApproverAuthorizationService.
/// </summary>
public class Adjustment : TenantScopedEntity
{
    public Guid OriginalTransactionId { get; set; }

    public Guid BranchId { get; set; }

    public Guid RequestedByUserId { get; set; }

    public Guid ApprovedByUserId { get; set; }

    public string Reason { get; set; } = string.Empty;

    /// <summary>What the returned line(s) were worth at the original sale's price.</summary>
    public decimal ReturnedTotal { get; set; }

    /// <summary>What the replacement line(s) cost at today's price.</summary>
    public decimal ReplacementTotal { get; set; }

    /// <summary>ReplacementTotal minus ReturnedTotal. Positive: the customer owes this much more.
    /// Negative: this much is owed back to the customer. Zero: a plain like-for-like swap, nothing
    /// changes hands.</summary>
    public decimal PriceDifference { get; set; }

    /// <summary>How the difference was settled — null when PriceDifference is zero.</summary>
    public PaymentMethod? SettlementMethod { get; set; }

    /// <summary>Cash tendered toward a positive difference, when SettlementMethod is Cash — mirrors
    /// Payment.AmountTendered.</summary>
    public decimal? SettlementAmountTendered { get; set; }
}
