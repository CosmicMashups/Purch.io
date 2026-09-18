using Purch.Domain.Common;
using Purch.Domain.Enums;

namespace Purch.Domain.Entities;

/// <summary>Automatic, no-code rule discounting a single item (percent off,
/// fixed amount off, or a flat override price) while the rule is active.</summary>
public class ItemDiscountPromoRule : TenantScopedEntity
{
    public string Name { get; set; } = string.Empty;

    public Guid ItemId { get; set; }

    public PromoDiscountType DiscountType { get; set; }

    public decimal DiscountValue { get; set; }

    public DateTimeOffset? StartsAt { get; set; }

    public DateTimeOffset? EndsAt { get; set; }

    public bool IsActive { get; set; } = true;
}
