using Purch.Domain.Common;
using Purch.Domain.Enums;

namespace Purch.Domain.Entities;

/// <summary>A cart-level code the cashier types in at checkout (D4/FR12) —
/// distinct from B2b's BundlePromoRule, which is an item-specific "buy N get
/// bundle price" rule with no code entry.</summary>
public class PromoCode : TenantScopedEntity
{
    public string Code { get; set; } = string.Empty;

    public PromoDiscountType DiscountType { get; set; }

    /// <summary>A percent (0-100) when DiscountType is Percentage, or a peso amount when FixedAmount.</summary>
    public decimal DiscountValue { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset? ExpiresAt { get; set; }
}
