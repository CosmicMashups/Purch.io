# Restock to purchase request: design (draft, awaiting owner decisions)

## Findings
- Bug fixed (commit on this branch): the dashboard restock list filtered `onHand > 0`, so out-of-stock items never showed. Out-of-stock (even with no threshold) now come first; `lowStockCount` still counts only items above zero. Ingredients likewise.
- A "purchase request" is the existing `PurchaseOrder` in `Draft` status (create, mark sent, cancel, receive via IRR already exist; Manager/Admin via `inventoryManager`). No new entity.
- Gaps: (1) no item/ingredient to supplier link exists anywhere, so "normally purchased from" has no data; (2) `PurchaseOrderLine` has `InventoryItemId` but `CreatePurchaseOrderService`/DTOs only accept `ItemId`, so ingredients cannot be ordered yet; (3) no "open order" lookup per item.
- Suggested qty already defined in the low-stock CSV: `max(threshold - onHand, 0)`.

## Proposed design
1. Add nullable `DefaultSupplierId` to `Item` and `InventoryItem` (migration, set in item/ingredient dialogs via existing supplier picker; Manager/Admin).
2. PO lines accept `InventoryItemId` (ItemId stays for items). Expected cost = item CostPrice / ingredient UnitCost, else 0.
3. `GET /inventory/restock-suggestions`: rows for out/low items and ingredients with suggested qty, default supplier, and `onOpenOrder` quantity (Draft/Sent/PartiallyReceived lines, outstanding = ordered - received).
4. `POST /purchase-orders/from-restock` body `{ branchId, lines:[{itemId|inventoryItemId, quantity, supplierId?}] }`: groups by supplier (explicit supplierId overrides default), one Draft PO per supplier, one transaction. Lines with no supplier are returned as `unassigned` and nothing is created for them.
5. Web (Restock first card): per-row "Order" action (single line, supplier picker if unassigned) and "Create purchase requests" bulk button (Manager/Admin only, hidden otherwise) opening a review dialog grouped by supplier with an Unassigned group where a supplier must be chosen or the lines skipped; quantities editable; rows already on an open order are unchecked by default with a note.

## Decisions needed from the owner
A. Add a default supplier on items and ingredients (recommended), or pick the supplier each time at order time?
B. Items already on an open order: skip by default (recommended) or include? Suggested qty = threshold - onHand minus open-order qty?
C. Unassigned items: must choose a supplier in the dialog, or just left out with a note (recommended: choose or skip)?
D. Include ingredients in this unit (needs line support) or items only first?
E. Overlap with U3: adds `DefaultSupplierId` and cost use; no conflict with valuation/stock-take/import, but import/export columns should later include supplier.

## Owner decisions (grill, 2026-10-15) - supersede the proposal above
- Supplier link: many suppliers per item and per ingredient (link table, RLS). One is marked preferred; auto-set when it is the only one. Support both modes: use the pre-selected preferred supplier automatically, or pick at order time.
- Bulk with several suppliers: always shown in the dialog, preferred pre-selected, user may change.
- Items already on an open order (Draft/Sent/PartiallyReceived): skipped entirely, shown as "On order".
- Ingredients included in this unit (PO lines + receiving must support InventoryItemId).
- No linked supplier: Unassigned group, choose any active supplier or Skip; offer to save the pick as preferred.
- Branch: dropdown in dialog, default active branch.
- Still open: per-supplier unit cost on the link, suggested qty rounding, Admin, Manager and Warehouse (the existing inventoryManager policy and Purchase Orders page roles).

## Tracking-on rule (owner, confirmed against ItemStockService)
- "Track ingredients separately" off: items only (as before).
- On: recipe items are never listed or ordered (availability comes from their ingredients; already excluded from stock counts). Ingredients are ordered. Non-recipe items (resale goods, stock held on the auto-paired record) remain orderable, as their paired record.

- Role gating: Admin, Manager and Warehouse (existing inventoryManager policy); web hides actions for others.
