using Purch.Domain.Common;

namespace Purch.Domain.Entities;

/// <summary>One line the customer is taking instead, priced at today's price (an item's price may have
/// moved since the original sale). Restricted to plain Unit and VariantMatrix items for now — see
/// AdjustmentService — so it never needs combo slot selections or modifier selections of its own.</summary>
public class AdjustmentReplacementLine : TenantScopedEntity
{
    public Guid AdjustmentId { get; set; }

    public Guid ItemId { get; set; }

    public Guid? ItemVariantId { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal LineTotal { get; set; }
}
