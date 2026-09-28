using Purch.Domain.Common;

namespace Purch.Domain.Entities;

/// <summary>
/// Proof that a batch of cart adds was applied: <see cref="Entity.Id"/> is the client's batch id. It is saved in the same
/// transaction as the lines, so a batch either has both or neither, and a retry that finds the receipt knows not to add
/// the lines again.
/// </summary>
public class CartBatchReceipt : TenantScopedEntity
{
    public Guid TransactionId { get; set; }
}
