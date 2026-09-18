namespace Purch.Application.Inventory;

public sealed record ItemRecipeLineDto(Guid InventoryItemId, string InventoryItemName, decimal? QuantityPerOrder);

public sealed record ReplaceItemRecipeLineRequest(Guid InventoryItemId, decimal? QuantityPerOrder);

public sealed record ReplaceItemRecipeRequest(IReadOnlyList<ReplaceItemRecipeLineRequest> Lines);
