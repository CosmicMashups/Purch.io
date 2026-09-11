using Purch.Domain.Common;
using Purch.Domain.Enums;

namespace Purch.Domain.Entities;

public class Item : TenantScopedEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Sku { get; set; }

    public string? Barcode { get; set; }

    public Guid? CategoryId { get; set; }

    public decimal BasePrice { get; set; }

    public string? ImageUrl { get; set; }

    public PricingType PricingType { get; set; }

    public decimal StockOnHand { get; set; }

    public bool IsActive { get; set; } = true;

    public Guid? DepartmentId { get; set; }

    /// <summary>Tingi (sub-unit) selling config — meaningful only when PricingType is WeightVolume.</summary>
    public TingiMode TingiMode { get; set; } = TingiMode.None;

    /// <summary>The whole/original pack size (e.g. 50 for a 50kg sack) — also sellable as-is alongside any tingi sizes.</summary>
    public decimal? PackagedSize { get; set; }

    /// <summary>Used when TingiMode is Increment — customer may order any whole multiple of this, up to PackagedSize.</summary>
    public decimal? TingiIncrementStep { get; set; }

    /// <summary>Used when TingiMode is FixedSizes — JSON array of decimals, e.g. [10, 25].</summary>
    public string? TingiAllowedSizesJson { get; set; }

    /// <summary>B2c — appointment/service length in minutes. Meaningful only when PricingType is Service.</summary>
    public int? ServiceDurationMinutes { get; set; }
}
