using Purch.Domain.Common;

namespace Purch.Domain.Entities;

public class ItemVariant : TenantScopedEntity
{
    public Guid ItemId { get; set; }

    /// <summary>e.g. {"size":"M","color":"Red"} — free-form per B3's size x color x custom attribute matrix.</summary>
    public string VariantAttributesJson { get; set; } = "{}";

    public string? Sku { get; set; }

    public decimal StockOnHand { get; set; }

    public decimal? PriceOverride { get; set; }

    public string? ImageUrl { get; set; }
}
