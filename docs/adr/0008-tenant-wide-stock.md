# 0008: Stock is tenant-wide, not per branch

**Status:** Accepted (U3 design, 2026-10-15)

Source: [engineering/specs/2026-10-15-u3-inventory-depth-design.md](../engineering/specs/2026-10-15-u3-inventory-depth-design.md),
"Constraints and decisions"; confirmed as an owner decision in [engineering/roadmap.md](../engineering/roadmap.md).

**Context:** `Item.StockOnHand` and `InventoryItem.QuantityOnHand` are single values. Branch exists only on
`InventoryMovement`.

**Decision:** U3 valuation, COGS, stock-take and import work on tenant-wide stock. No per-branch valuation, count or
import.

**Consequences:** per-branch stock is a separate future unit (listed as an open follow-up in the roadmap). A stock
import still records a branch on the movements it posts, so the commit takes a `BranchId`.
