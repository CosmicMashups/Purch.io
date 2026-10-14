using Purch.Domain.Enums;
using Purch.Domain.Common;

namespace Purch.Domain.Entities;

public class TransactionLine : TenantScopedEntity
{
    public Guid TransactionId { get; set; }

    /// <summary>Null for an open item (a one-off line with no catalog item).</summary>
    public Guid? ItemId { get; set; }

    public Guid? ItemVariantId { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal LineTotal { get; set; }

    /// <summary>Discount applied by an automatic item-level promo (BOGO/combo/item
    /// discount), for receipt/cart display. Recomputed from scratch on every
    /// RecalculateTotalAsync call — see TransactionService.ApplyItemPromosAsync.</summary>
    public decimal PromoDiscountAmount { get; set; }

    /// <summary>e.g. "BUY 1 TAKE 1", "COMBO ₱85.00", "20% OFF" — null when no
    /// automatic item promo applies to this line.</summary>
    public string? AppliedPromoLabel { get; set; }

    /// <summary>Cost of one unit at the time of sale; null when unknown.</summary>
    public decimal? UnitCost { get; set; }

    /// <summary>A cashier-entered discount on this line.</summary>
    public decimal ManualDiscountAmount { get; set; }

    /// <summary>How the discount was entered (null = none). The amount above is derived from this on every recalculation.</summary>
    public ManualDiscountKind? ManualDiscountKind { get; set; }

    public decimal? ManualDiscountValue { get; set; }

    /// <summary>The catalog price this line's UnitPrice was overridden from, or null.</summary>
    public decimal? PriceOverrideFrom { get; set; }

    public bool IsOpenItem { get; set; }

    public string? OpenItemName { get; set; }

    public string? Note { get; set; }
}
