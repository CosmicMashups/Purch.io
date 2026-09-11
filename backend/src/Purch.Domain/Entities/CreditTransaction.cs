using Purch.Domain.Common;

namespace Purch.Domain.Entities;

public class CreditTransaction : TenantScopedEntity
{
    public Guid CustomerCreditLedgerId { get; set; }

    public Guid? TransactionId { get; set; }

    public decimal Amount { get; set; }

    public string? Note { get; set; }
}
