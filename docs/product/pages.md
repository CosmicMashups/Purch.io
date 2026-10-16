# Purch.io — Page & Component Specification
*Fit-to-all POS system. For Google Stitch design handoff. Style direction: corporate minimalist, high-contrast, large touch targets, minimal text, icon-forward — designed for computer-illiterate staff on Android/iOS tablets.*

**Orientation: Landscape**, applies to all Admin, POS Cashier, Inventory, and Reporting screens (Sections A–D, F, G) — designed for tablet-in-a-stand use. Section E (Self-Service Kiosk) is **Portrait**, per PH QSR kiosk convention.

---

## Design Principles (apply to every page)
- Large tap targets (min 48x48dp), generous spacing, no dense menus
- One primary action per screen, always visible, always in the same corner
- Color = meaning (green = paid/success, red = stock-out/error, amber = warning), never decorative only
- Merchant branding (logo, theme color, font) applied globally — design with placeholder tokens, not hardcoded brand
- Every destructive action (delete, void, refund) requires a confirm step

---

## A. Merchant Onboarding & Configuration (Admin, one-time + ongoing)

### A1. Business Type Selector
- Grid of business type cards (Restaurant, Café, Clothing Shop, Department Store, Convenience Store, Grocery, Sari-Sari Store, Other/Custom)
- Selecting a type pre-configures downstream modules (combo builder, variant matrix, weight/unit pricing, etc.)

### A2. Branding Setup
- Logo uploader (with live preview on mock receipt + mock POS header)
- Color theme picker (primary/secondary, with contrast-safety warning)
- Font selector (dropdown of pre-approved web-safe/legible fonts)
- Live preview panel showing POS home screen with applied branding

### A3. Branch & Device Setup
- Branch list (name, address, active device count)
- Add branch form
- Device pairing screen (QR code / pairing code flow)

### A4. Staff & Roles Management
- Staff list table (name, role, branch, status)
- Add/edit staff form
- Role permission matrix (Admin, Manager, Cashier, Warehouse/Storekeeper)

### A5. BIR / Compliance Settings
- Receipt numbering sequence config (tamper-evident, sequential, gap-audit view)
- TIN, business name, registered address fields
- Z-reading / X-reading schedule settings
- Senior Citizen / PWD discount rule config (mandated receipt formatting)
- Data privacy / retention settings (customer data, utang ledger, staff records)

### A6. Security & Access Settings
- Device pairing + PIN/staff-login requirement toggle
- Auto-logout / screen-lock idle timer
- Role permission matrix editor (server-enforced, not just UI visibility)
- Audit log viewer (price overrides, voids, refunds, inventory adjustments — filterable by staff/date/action type)

---

## B. Item & Catalog Management (Admin)

### B1. Item Catalog List
- Searchable/filterable table: item name, category, price, stock status, image thumbnail
- Bulk actions (activate/deactivate, bulk price update)

### B2. Add/Edit Item — Base Form
- Name, SKU/barcode, category, base price, image, description
- **Pricing type selector**: Unit / Weight-Volume / Bundle / Service / Combo / Variant-Matrix — drives which of B3–B7 appears next
- Barcode scanner input support (USB/Bluetooth) alongside manual entry

### B2a. Weight/Volume Pricing Module (Grocery/Convenience mode)
- Price-per-unit field (per kg/liter/etc.), scale integration hookup
- Batch/lot entry with expiry date, FIFO stock-out flag
- Sachet/tingi (small-pack) price vs. bulk price, linked to same base product

### B2b. Bundle/Promo Pricing Module (Grocery/Convenience/Retail mode)
- Multi-buy rule builder ("Buy 2 Get 1", "3 for ₱X")
- Bundle component picker + bundle price override

### B2c. Service/Appointment Item Module (Salon, repair shop, "other establishments")
- Duration field instead of stock quantity
- Linked staff/resource assignment
- Scheduling slot config (feeds a future booking calendar, out of MVP scope unless needed)

### B3. Add/Edit Item — Variant Module (Clothing/Retail mode)
- Variant matrix builder: size × color × custom attribute
- Per-variant SKU, stock count, price override
- Variant image swatches

### B4. Add/Edit Item — Combo/Meal Builder (Restaurant/Café mode)
- "Single item" vs "Combo" toggle
- Combo component picker (choose category + quantity, e.g. "1 Main + 1 Side + 1 Drink")
- Per-slot substitution/upgrade pricing rules

### B5. Category & Modifier Management
- Category list (drag to reorder — affects POS grid order)
- Modifier groups (add-ons, extras, spice level, etc.) reusable across items

### B6. Department/Concessionaire Management (Department Store mode)
- Department list, each with own inventory attribution and sales reporting
- Assign items to department; shared checkout, split sales-attribution report

### B7. Customer Credit Ledger Setup (Sari-Sari Store mode)
- Enable/disable "utang" (tab/credit) feature toggle
- Credit limit per customer, due-date/reminder settings

### B8. Ingredient-Level Inventory & Recipes (opt-in, any vertical — e.g. Café/Restaurant)
- Business Settings toggle: "Track inventory separately from menu items" — off by default, purely additive
- Recipe editor reachable from an item's edit screen (visible only when the toggle is on): checklist of Inventory Items used by this menu item, each with an optional quantity-per-order field (blank = just check availability, filled = auto-consume that much per sale)
- See C6 for managing the Inventory Items (ingredients) themselves

### B9. Promos (Admin/Manager)
- Tabbed screen: Promo Codes (existing code-entry discounts) alongside three automatic, no-code-entry promo types:
  - **Buy 1 Take 1**: trigger item + quantity → free item + quantity (same or different item)
  - **Combo Deals**: two specific items priced as one fixed total when bought together
  - **Item Discounts**: percentage off / fixed amount off / fixed override price for a specific item
- Each automatic promo type carries a start/end date-time window and an active toggle; all three apply themselves at checkout with no cashier action required

---

## C. Inventory Management (Admin / Warehouse Officer / Storekeeper)

### C1. Inventory Dashboard
- Stock overview cards: total SKUs, low-stock alerts, out-of-stock count, pending movements
- Low-stock alert list with reorder shortcut

### C2. Stock Movement Log
- Filterable table: date, item, movement type, quantity, branch, staff, reason/note
- Movement type filter chips: Stock-In, Stock-Out, Consumption, Spoiled, Damaged, For Return, Transfer, Adjustment, **Sale** (system-generated only, from a completed Cashier sale — never a manual entry option in C3)

### C3. Record Movement — Form (per type)
- Shared fields: item/variant picker, quantity, branch, date, note
- Type-specific fields (e.g. "Spoiled" requires reason category + photo attachment optional; "For Return" requires supplier/reference)

### C4. Stock Transfer (Multi-Branch)
- Source branch → destination branch picker
- Item/quantity list, transfer status tracker (Pending / In Transit / Received)

### C5. Supplier & Purchase Order Management
- Supplier list
- Create PO form, PO status tracker, receive-stock-against-PO flow

### C6. Inventory Items (ingredients, opt-in — see B8)
- List of Inventory Items with name, unit of measure (base unit e.g. mL/g/pc), packaging (e.g. "450 mL per pc"), quantity on hand shown as whole packages + partial remainder, and a stock-level badge
- Create/edit form: name, SKU, base unit, packaging unit + size, low-stock threshold
- Per-item actions: **Physical Count** (manually set quantity on hand, e.g. for items with no recipe attached to any menu item) and **Receive Stock** (add whole packages from a delivery, e.g. "received 10 pcs")

---

## D. Point of Sale — Cashier View (Tablet, primary daily-use screen)

### D0. Home Tab (bottom nav, tab 1 — every role) *(shipped)*
- Launchpad, not a list: greeting header, a large "New Sale" CTA card, a couple of quick-action shortcuts (Shift/Drawer, Payment Reminders)
- "Needs your attention" section surfaces unsynced/flagged records only when there are any — no empty banner when there's nothing to review

### D1. POS Home / Item Grid (reached via the Sell tab)
- Category tab bar (top, scrollable)
- Item grid (large tiles, image + name + price)
- Order summary sidebar (running cart, quantity steppers, subtotal)
- Persistent "Charge" button (bottom-right, always visible)

### D2. Item Customization Sheet — Combo Mode (Restaurant/Café)
- Slot-by-slot selection (Main → Side → Drink)
- Modifier checkboxes (extras, spice level)
- "Add to order" confirm button

### D3. Item Customization Sheet — Variant Mode (Clothing/Retail)
- Size selector (chip row)
- Color selector (swatch row)
- Stock indicator per variant combination (greys out if out-of-stock)

### D4. Cart / Order Review
- Line items with edit/remove — each line also shows, where applicable: selected variant attributes, combo slot picks, modifier chips, and an applied-promo chip (e.g. "BUY 1 TAKE 1", "COMBO ₱85.00", "20% OFF") for a line automatically discounted by an active promo from B9
- Discount/promo code entry, Senior/PWD discount toggle (auto-recalculates tax per BIR rule), plus a read-only "Item promos" total reflecting whatever automatic BOGO/combo/item-discount promos (B9) matched the cart — no cashier action needed to apply them, they stack automatically ahead of Senior/PWD and the promo code
- Order type selector (Dine-in / Takeout / Delivery — restaurant mode only)
- **Void/refund/discount-after-sale** as distinct permissioned actions, each logged to audit log

### D5. Payment Screen
- Payment method tabs: Cash, Card, GCash/Maya, QR Ph, Bank Transfer, **Bill Payment/E-Load** (convenience store mode), **Utang/Credit** (sari-sari mode, deducts from customer credit ledger)
- Cash: tendered amount pad + change calculator, cash drawer trigger on complete
- Digital: QR display / terminal-linked confirmation state
- Split payment option

### D7. Shift Handover / Cash Drawer Reconciliation (Convenience Store mode, also available generally)
- Opening/closing cash count entry
- Expected vs. actual variance display, approval flow for discrepancies
- Handover summary between outgoing/incoming cashier

### D6. Receipt / Order Complete
- Success confirmation (large checkmark, order number)
- Print receipt / send digital receipt (SMS/email) buttons
- "New Order" primary action

---

## E. Self-Service Kiosk (optional module, restaurant/café/QSR mode)
**Orientation: Portrait** (matches PH QSR kiosk convention — Jollibee/McDonald's-style).

### E1. Kiosk Landing / Idle Screen
- Branding hero, "Tap to Order" large CTA, language selector

### E2. Kiosk Category Carousel
- Large swipeable category cards

### E3. Kiosk Item Customize Page
- Same combo/variant logic as D2/D3, kiosk-optimized (larger touch targets, no cashier assumed)

### E3a. Kiosk Cart Review
- Order review before the fulfillment choice — same per-line detail as D4 (variant attributes, combo picks, modifier chips, applied-promo chip), read-only since the kiosk has no discount/promo-entry controls of its own
- Quantity/remove per line, running total, "Continue" to E4

### E4. Kiosk Fulfillment Choice
- Staff-assist vs self-fetch vs dine-in/takeout selector

### E5. Kiosk Payment
- Same method set as D5, kiosk-optimized flow, idle timeout handling

### E6. Kiosk Order Confirmation / Queue Number
- Large order/queue number display, estimated wait time

---

## F. Reporting & Analytics (Admin/Manager)

### F1. Sales Dashboard
- Revenue summary cards (today/week/month), trend chart
- Top-selling items list
- Branch comparison view (multi-branch merchants)

### F2. Shift / Cash Drawer Report
- Opening/closing cash, expected vs actual, discrepancy flag
- Z-reading / X-reading report (BIR-formatted)

### F3. Inventory Reports
- Stock movement summary by type (stock-in/out, spoilage, damage, returns) over date range
- Low-stock / reorder report export

### F4. Staff Performance Report
- Sales per cashier, shift attendance summary

---

## G. Shared / System-Wide Components

- **App shell / navigation** *(shipped)*: a persistent Material 3 bottom `NavigationBar` with up to five tabs — Home, Sell, Reports, Inventory, Business — each keeping its own independent navigation stack. Which tabs appear is role-based and server-claim-driven, not just hidden client-side: Admin and Manager see all five (Manager loses two Admin-only tiles under Business); Cashier sees only Home + Sell; Warehouse sees only Home + Inventory. Dense tabs (Inventory, Business) group their destinations into labeled sections rather than one flat list.
- **Offline indicator banner**: persistent small banner when device is offline, with pending-sync count
- **Confirmation modal**: reusable for all destructive/critical actions
- **Empty states**: illustrated, plain-language (e.g. "No items yet — tap + to add your first item")
- **Toast/notification system**: success (green), warning (amber), error (red)
- **Search bar component**: reused across catalog, inventory, staff, reports
- **Date range picker**: reused across all reporting pages
- **Hardware status indicators**: barcode scanner, receipt printer, cash drawer connection status (icon row, header or settings)
- **License/entitlement banner**: shown if device/tenant license validation fails (relevant for subscription/per-transaction pricing model)

---

## Notes for MVP Scoping
Not every page above ships in v1. Recommended cutoff: build the core (A, B1–B2, B4 or B3 depending on pilot vertical, C, D, F1–F2) fully for **one** vertical tied to an actual client first. Treat B2a/B2b/B2c, B6, B7, E (kiosk), and multi-branch transfer (C4) as phase 2 unless a signed client requires them at launch.
