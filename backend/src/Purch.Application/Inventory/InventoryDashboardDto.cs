namespace Purch.Application.Inventory;

/// <summary>C1 — the overview cards + low-stock alert list. "Pending movements"
/// from PAGES.md isn't included yet — it belongs to C4/C5's transfer and
/// purchase-order status tracking, neither of which exists yet.</summary>
public sealed record InventoryDashboardDto(
    int TotalSkus,
    int OutOfStockCount,
    int LowStockCount,
    IReadOnlyList<LowStockItemDto> LowStockItems,
    IngredientStockDto? Ingredients = null);

/// <summary>The same counts for separately tracked ingredients. Only present when the business tracks ingredients
/// separately. The stock records auto-paired to plain items are left out: they already count as items.</summary>
public sealed record IngredientStockDto(
    int Total,
    int OutOfStockCount,
    int LowStockCount,
    IReadOnlyList<LowStockIngredientDto> LowStock);

public sealed record LowStockIngredientDto(
    Guid InventoryItemId,
    string Name,
    string BaseUnit,
    decimal QuantityOnHand,
    decimal LowStockThreshold);

public sealed record LowStockItemDto(
    Guid ItemId,
    string ItemName,
    decimal StockOnHand,
    decimal LowStockThreshold);
