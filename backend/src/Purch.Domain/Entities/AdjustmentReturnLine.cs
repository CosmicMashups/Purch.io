using Purch.Domain.Common;

namespace Purch.Domain.Entities;

/// <summary>One line the customer is returning, priced at the original sale's unit price (not today's
/// price — what they paid is what they're owed credit for). OriginalLineId ties it back to the specific
/// TransactionLine it reduces the claim against, so a second, later exchange on the same sale can't
/// return more of that line than is left.</summary>
public class AdjustmentReturnLine : TenantScopedEntity
{
    public Guid AdjustmentId { get; set; }

    public Guid OriginalLineId { get; set; }

    public Guid ItemId { get; set; }

    public Guid? ItemVariantId { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal LineTotal { get; set; }
}
