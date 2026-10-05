namespace Purch.Application.Catalog;

public sealed record ModifierGroupDto(
    Guid Id,
    string Name,
    bool AllowMultipleSelection,
    bool IsRequired,
    IReadOnlyList<ItemModifierDto> Modifiers);

/// <summary>IsOutOfStock is the server's verdict from the modifier's ingredients, so a kiosk can dim the
/// option without ever reading inventory quantities. Ingredients are the names and amounts behind it.</summary>
public sealed record ItemModifierDto(
    Guid Id,
    string Name,
    decimal PriceDelta,
    bool IsOutOfStock = false,
    IReadOnlyList<ModifierIngredientDto>? Ingredients = null);

public sealed record ModifierIngredientDto(Guid InventoryItemId, string InventoryItemName, decimal? QuantityPerOrder);

public sealed record ReplaceModifierIngredientLine(Guid InventoryItemId, decimal? QuantityPerOrder);

public sealed record ReplaceModifierIngredientsRequest(IReadOnlyList<ReplaceModifierIngredientLine> Lines);

public sealed record UpdateItemModifierRequest(string Name, decimal PriceDelta);
