namespace Purch.Application.Inventory;

/// <summary>C1 — the overview cards + low-stock alert list. "Pending movements"
/// from PAGES.md isn't included yet — it belongs to C4/C5's transfer and
/// purchase-order status tracking, neither of which exists yet.</summary>
public sealed record InventoryDashboardDto(
    int TotalSkus,
    int OutOfStockCount,
    int LowStockCount,
    IReadOnlyList<LowStockItemDto> LowStockItems);

public sealed record LowStockItemDto(
    Guid ItemId,
    string ItemName,
    decimal StockOnHand,
    decimal LowStockThreshold);
