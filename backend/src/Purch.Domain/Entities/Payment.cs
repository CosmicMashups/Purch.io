using Purch.Domain.Common;
using Purch.Domain.Enums;

namespace Purch.Domain.Entities;

public class Payment : TenantScopedEntity
{
    public Guid TransactionId { get; set; }

    public PaymentMethod Method { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    public decimal Amount { get; set; }

    public decimal? AmountTendered { get; set; }

    public decimal? ChangeGiven { get; set; }
}
