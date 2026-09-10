# Purch.io — System Workflow (Non-Technical)
*Speculative baseline — no signed client yet, walkthrough uses retail/sari-sari as the reference vertical.*

## 1. Getting Set Up (Owner, one-time)
The owner signs up, selects a business type, and the system pre-arranges screens to match. Owner uploads logo, picks a theme color, chooses a font — applied globally to POS screens and receipts.

## 2. Stocking the Shelves (Owner or Storekeeper)
Each product is added with name, photo, price, counting method (piece/weight/etc.), and current stock on hand. This becomes the source of truth that every later sale subtracts from automatically.

## 3. A Typical Sale (Cashier)
Cashier taps items on a picture grid, building a running cart. Taps "Charge," selects payment method (cash, GCash, card, etc.), system calculates change if cash. Receipt prints or sends digitally. Stock decrements automatically in the background.

## 4. When the Internet Drops
Tablet keeps working offline, queuing sales locally, and syncs automatically once connection returns — cashier doesn't need to do anything differently.

## 5. Restocking and Shrinkage (Owner or Storekeeper)
Deliveries logged as stock-in. Spoilage, damage, and returns each logged as their own event with a reason — giving the owner a real picture of *why* stock disappears, not just that it did.

## 6. End of Day (Owner/Manager)
Dashboard shows total sales, best sellers, and cash-drawer reconciliation (expected vs. actual, discrepancies flagged). Required government paperwork (sequential receipts, daily summary reports) is generated automatically in the background.

## 7. Multiple Staff, Controlled Access
Cashiers ring up sales only. Storekeepers log inventory movements. Only owner/manager can void transactions, apply after-the-fact discounts, or view full reports — every sensitive action leaves a timestamped trail of who did what.

## 8. Growing Beyond One Type of Store
The same core engine reconfigures per business type — a café gets a combo-builder, a clothing shop gets size/color pickers — the customer-facing experience changes, the underlying engine doesn't.

---
**Status note:** This is the designed workflow, not a validated one. Which pieces ship first is still undecided pending a signed client.
