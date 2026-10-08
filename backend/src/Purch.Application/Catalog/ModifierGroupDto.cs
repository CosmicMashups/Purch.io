namespace Purch.Application.Catalog;

public sealed record ModifierGroupDto(
    Guid Id,
    string Name,
    bool AllowMultipleSelection,
    bool IsRequired,
    IReadOnlyList<ItemModifierDto> Modifiers,
    Guid? CategoryId = null,
    IReadOnlyList<ModifierCategoryItemDto>? CategoryItems = null,
    bool IsActive = true);

/// <summary>A category item offered through a category-linked group. Price is what the customer pays
/// (the group's override, else the item's own); IsExcluded items are listed so admins can restore them, and
/// clients skip them when ordering. IsOutOfStock is the server's verdict, like <see cref="ItemModifierDto"/>.</summary>
public sealed record ModifierCategoryItemDto(
    Guid ItemId,
    string Name,
    string? ImageUrl,
    decimal BasePrice,
    decimal? PriceOverride,
    decimal Price,
    bool IsExcluded,
    bool IsOutOfStock);

public sealed record UpdateModifierGroupRequest(string Name, bool AllowMultipleSelection, bool IsRequired, Guid? CategoryId);

public sealed record UpdateModifierCategoryItemRequest(decimal? PriceOverride, bool IsExcluded);

/// <summary>IsOutOfStock is the server's verdict from the modifier's ingredients, so a kiosk can dim the
/// option without ever reading inventory quantities. Ingredients are the names and amounts behind it.</summary>
public sealed record ItemModifierDto(
    Guid Id,
    string Name,
    decimal PriceDelta,
    bool IsOutOfStock = false,
    IReadOnlyList<ModifierIngredientDto>? Ingredients = null,
    bool IsActive = true);

public sealed record ModifierIngredientDto(Guid InventoryItemId, string InventoryItemName, decimal? QuantityPerOrder);

public sealed record ReplaceModifierIngredientLine(Guid InventoryItemId, decimal? QuantityPerOrder);

public sealed record ReplaceModifierIngredientsRequest(IReadOnlyList<ReplaceModifierIngredientLine> Lines);

public sealed record UpdateItemModifierRequest(string Name, decimal PriceDelta);
