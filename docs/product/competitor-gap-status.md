# Competitor gap status

Status: Living · Audited: 2026-10-16 against `main` (HEAD `1951ce7`) · Audience: product owner, contributors

This audit checks the P1/P2/P3 competitor-gap list (Loyverse, Square, Lightspeed, Shopify POS, Toast, Zoho Inventory) against the code that ships today. It was done before the tiers, limits and free-features work, because tier contents depend on which gaps are already closed.

Scope is the backend and the web client. The Flutter client is unchanged since before U1 and is tracked as U6.

How to read it: **Shipped** means built, tested and in the feature inventory. **Partial** means part of the gap is closed. **Open** means no code found (grep of `backend/src` and `web/src`). Priorities (P1, P2, P3) are the original ones. Feature IDs point to [feature-inventory.md](feature-inventory.md).

## Summary

- All P1 selling, inventory and reporting gaps are shipped (U1, U3, U4).
- Five P1 gaps remain: attach a customer to a sale, loyalty, customer profile (CRM), time clock and custom roles. They are units U2 and U5, which have no spec yet.
- Subscription billing (P1) is the monetization work itself. The hooks exist but nothing reads them (see the last section).
- Every P2 and P3 item is open, except partials noted below.

## Selling / POS

| Gap | Pri | Status | Evidence |
|---|---|---|---|
| Park several carts | P1 | Shipped | POS-15, POS-16, POS-17 |
| Split payment (multi-tender) | P1 | Shipped | POS-21. Split bill by table stays deferred to table management |
| Manual line and cart discounts, price override | P1 | Shipped | POS-18, POS-19. Every use needs a Manager or Admin approver PIN |
| Open item / open price | P1 | Shipped | POS-20, tenant toggle ONBD-22 |
| Attach a customer to a sale (non-credit) | P1 | Open | `Transaction.CustomerId` column exists, but there is no Customer entity or picker (U2). Only the utang ledger can be charged |
| Loyalty points and rewards | P1 | Open | No code (U2) |
| Tips and service charge | P2 | Open | No code |
| Gift cards, store credit | P2 | Open | No code. Utang ledger is customer debt, not stored value |
| Email, SMS or QR digital receipt | P2 | Open | Receipts are printed only. No mail or SMS provider |
| Table and floor management, open tabs | P2 | Open | Dine In / Take Out only. Park-cart note can hold a label such as "Table 4" |
| Item notes per line | P2 | Shipped | POS-20 (line note) |
| Non-VAT, zero-rated, several tax rates | P2 | Partial | VATable, VAT-exempt and zero-rated lines are stamped (REP-10), but one rate per country (12% PH) and no per-item tax setting |
| Card terminal and e-wallets (GCash, Maya direct) | P2 | Partial | Manual GCash QR and bank transfer work. QR Ph and Dragonpay bill-pay are modelled but not connected (ADR 0004) |
| Delivery, online store, GrabFood or foodpanda | P3 | Open | Kiosk self-order exists (KIOSK-01..04); no online ordering |

## Inventory and purchasing

| Gap | Pri | Status | Evidence |
|---|---|---|---|
| Item cost, COGS, margin, valuation | P1 | Shipped | CAT-19, INV-17, INV-18, REP-11 |
| Bulk import and export of items and stock | P1 | Shipped | INV-21, INV-22 (CSV, not Excel) |
| Stock-take sessions with variance | P1 | Shipped | INV-19, INV-20 |
| Barcode and label printing, SKU generation | P2 | Open | No label code. Barcode and SKU fields exist |
| Reorder suggestions, auto-PO | P2 | Partial | Restock list, purchase requests, approve, convert to purchase orders. No automatic PO and no sales-velocity forecast |
| Supplier cost history, lead time | P3 | Partial | Current `ItemSupplier.UnitCost` and PO line costs only. No history table, no lead time |
| Catalog units of measure, price lists, serials | P3 | Open | Item-level unit and tingi only |
| Low-stock and expiry notifications | P2 | Open | Dashboard only. No email or push |

## Customers and marketing

| Gap | Pri | Status | Evidence |
|---|---|---|---|
| Customer profile and CRM | P1 | Open | Only the utang ledger (CRED-01..06) |
| Segments and targeted promos | P3 | Open | |
| SMS or email marketing, feedback | P3 | Open | |

## Staff and roles

| Gap | Pri | Status | Evidence |
|---|---|---|---|
| Time clock, attendance | P1 | Open | Shift-based counts only (REP-07). No clock in or out |
| Custom roles, granular permissions | P1 | Open | Fixed roles. `Permissions.For(Role)` is hard-coded; its comment says U5 makes it data-driven |
| Scheduling, commissions | P3 | Open | |

## Reports

| Gap | Pri | Status | Evidence |
|---|---|---|---|
| Sales by payment type, category, hour; tax report | P1 | Shipped | REP-09, REP-10 |
| Gross profit, discount, void, refund reports | P1 | Shipped | REP-11, REP-12, REP-13 |
| Period-over-period, scheduled email reports | P2 | Open | Only the retention cron runs on a schedule |
| Excel and PDF export, per-shift print | P2 | Partial | CSV plus Save as PDF (browser print) on reports; no Excel; X and Z readings print |
| Owner alerts, daily summary push | P2 | Open | No notification channel |

## Platform and integrations

| Gap | Pri | Status | Evidence |
|---|---|---|---|
| Accounting export or integration | P2 | Open | Raw CSV exports only |
| Public API and webhooks | P3 | Open | |
| Subscription billing, plans, trial | P1 | Open | The monetization task. Hooks listed below |
| Import from competitors (Loyverse or Square CSV) | P2 | Open | The CSV import takes Purch.io's own format |
| Multi-language, multi-currency | P3 | Open | One country profile (PH, PHP); web is English only |

## Where Purch.io already leads

Offline-capable sync with conflict review, BIR X and Z readings with sequential receipts, Senior and PWD discounts, tingi (sachet) selling, utang ledger with privacy erase, kiosk, kitchen display, order board and customer display, recipes and batches with expiry, concessionaire departments, on-premises mode, audit log.

## What this means for tiering

- A free tier is credible today: everything a shop needs to sell, count stock and report is built.
- Before charging for depth, decide whether the five open P1 items ship first (customer attach, loyalty, CRM, time clock, custom roles). Square and Loyverse treat the first three as free core.
- Existing hooks, all unread by any gate:
  - `Tenant.LicenseStatus` (Trial, Active, Suspended).
  - `Tenant.FeatureFlagsJson` (default `{}`).
  - `TenantMetering` (monthly transaction count; table only).
- No tier, plan, quota or entitlement code exists. `Permissions.For` is the natural place for role limits once U5 makes it data-driven.
- The P1 items that carry scale limits are already measurable: branches, devices, staff accounts, items, report history range. Stock is tenant-wide (ADR 0008), so "per location" limits count branches and devices, not stock.

## Corrections noted during the audit

- `docs/engineering/roadmap.md` still has older parked-item sections. The owner has since resolved the U1 and U3 parked items (commits `e1a85fc`, `375a33a`, `de49453`). Refresh the roadmap when convenient.
- Spec headers for U1 and U3 were marked as shipped in `e1a85fc`; `history/doc-conflicts.md` item C11 can be closed.
