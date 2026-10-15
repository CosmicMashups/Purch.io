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
