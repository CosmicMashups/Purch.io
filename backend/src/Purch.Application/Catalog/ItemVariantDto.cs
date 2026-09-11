namespace Purch.Application.Catalog;

public sealed record ItemVariantDto(
    Guid Id,
    IReadOnlyDictionary<string, string> Attributes,
    string? Sku,
    decimal StockOnHand,
    decimal? PriceOverride,
    string? ImageUrl);

/// <summary>Attributes example: {"size":"M","color":"Red"} — free-form per B3's size x color x custom attribute matrix.</summary>
public sealed record CreateItemVariantRequest(
    IReadOnlyDictionary<string, string> Attributes,
    string? Sku,
    decimal? PriceOverride,
    string? ImageUrl);
