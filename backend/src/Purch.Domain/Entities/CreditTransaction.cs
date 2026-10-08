using Purch.Domain.Common;

namespace Purch.Domain.Entities;

public class CreditTransaction : TenantScopedEntity
{
    public Guid CustomerCreditLedgerId { get; set; }

    public Guid? TransactionId { get; set; }

    public decimal Amount { get; set; }

    public string? Note { get; set; }

    /// <summary>The shift a repayment was collected in, so the cash counts toward that drawer. Null for a charge made
    /// by a sale and for the reversal written when a credit sale is refunded.</summary>
    public Guid? ShiftId { get; set; }
}
