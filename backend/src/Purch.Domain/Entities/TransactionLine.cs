using Purch.Domain.Common;

namespace Purch.Domain.Entities;

public class TransactionLine : TenantScopedEntity
{
    public Guid TransactionId { get; set; }

    public Guid ItemId { get; set; }

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
}
