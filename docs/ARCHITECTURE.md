# Purch.io — Architecture Document

## 1. System Overview
Purch.io is a multi-tenant, offline-first POS platform with a config-driven vertical layer. Three logical layers:

1. **Client** — Flutter app (Android/iOS tablets), landscape (POS/admin) and portrait (kiosk, phase 2)
2. **API** — stateless backend, multi-tenant, exposes REST/GraphQL to client
3. **Data** — Postgres (relational core: tenants, items, transactions, inventory movements) + local SQLite on-device for offline queue

```
[Flutter Client] <--offline queue/sync--> [Local SQLite]
        |
        | REST/GraphQL (online)
        v
   [Backend API] --- [Postgres] 
        |
        +--- [Object storage: logos, item images]
        +--- [BIR receipt-numbering service]
        +--- [Payment provider integrations: GCash/Maya/QR Ph/card processors]
```

## 2. Client Architecture (Flutter)
- **State management:** Riverpod or Bloc (pick one, be consistent — Riverpod recommended for smaller team, less boilerplate)
- **Local persistence:** `drift` (typed SQLite) for offline transaction queue, cached catalog, cached branding config
- **Sync engine:** background isolate, timestamp-based conflict resolution (later-timestamped order auto-cancelled on conflict, per earlier decision) — build the schema/queue table now even if full sync logic ships in phase 2
- **Navigation:** role-aware routing (cashier vs. admin vs. warehouse officer see different route trees)
- **Theming:** dynamic `ThemeData` built at runtime from tenant's branding config (logo URL, color hex, font family) fetched on login/sync — not hardcoded per build
- **Hardware integration points (stubbed in v1, wired in as needed):** barcode scanner (platform channel or `flutter_barcode_scanner`-class package), receipt printer (Bluetooth/USB thermal, e.g. `esc_pos_bluetooth`-class package), cash drawer (printer-triggered kick)

## 3. Backend Architecture
- **Stack:** framework-agnostic recommendation — Node.js/NestJS or ASP.NET Core (Minimal API) both fit; ASP.NET Core keeps you inside the C# ecosystem you're learning and pairs cleanly with a free-tier-friendly host (Render, Railway, or Azure free tier for App Service)
- **Multi-tenancy:** shared database, `tenant_id` column on all tenant-scoped tables (not per-tenant schema — cheaper to run at $0-tier, adequate at SME scale; revisit only if a single tenant needs data isolation guarantees a shared DB can't meet)
- **API shape:** REST for CRUD (catalog, inventory, staff), consider a dedicated sync endpoint (`POST /sync`) that accepts a batch of queued offline transactions and returns conflict resolutions in one round trip — don't make the client sync item-by-item
- **Auth:** JWT-based, role claims embedded (Admin/Manager/Cashier/Warehouse), server-side permission checks on every endpoint (not just UI hiding — see security notes)
- **Config layer:** `business_type` + `feature_flags` table per tenant, read by both backend (validation rules) and client (which UI modules render). Larger opt-in behavior changes get their own first-class boolean on `tenants` rather than being folded into the generic flags blob — e.g. `use_separate_inventory_tracking`, which switches a tenant's stock model from direct `items.stock_on_hand` to the ingredient-level Inventory Item/Recipe model below, entirely additively (off by default, existing tenants unaffected).

## 4. Data Model (core entities, not exhaustive)
- `tenants` (business_type, branding config, feature_flags, license_status)
- `branches`, `devices`
- `users` (role, branch, pin/login)
- `items` (pricing_type: unit/weight/bundle/service/combo/variant, base fields)
- `item_variants` (for variant-matrix pricing_type)
- `item_combo_components` (for combo pricing_type)
- `inventory_movements` (item_id or inventory_item_id, branch_id, type: stock-in/stock-out/consumption/spoiled/damaged/for-return/transfer/adjustment/**sale**, quantity, staff_id, timestamp, note) — `sale` is logged automatically by a completed Cashier sale (never a manual entry), kept distinct from a manual `stock-out` or a recipe's `consumption` so the movement log can tell all three apart
- `inventory_items` (tenant-scoped ingredient/stock record, used only when `tenants.use_separate_inventory_tracking` is on: name, base_unit e.g. mL/g/pc, packaging_unit + packaging_size e.g. 450 mL per pc, quantity_on_hand always in base_unit, low_stock_threshold, and a `linked_item_id` back-reference for the 1:1 fallback record auto-created for a catalog item with no recipe — preserving today's manual Physical Count behavior)
- `item_recipe_lines` (the BOM: catalog item_id → inventory_item_id, with an optional quantity_per_order — null means "just check availability," set means "auto-consume this much per sale")
- `promo_codes` (cart-level, code-entry discount — percentage or fixed amount, admin-created, cashier-applied at checkout)
- `bogo_promo_rules`, `combo_promo_rules`, `item_discount_promo_rules` — automatic, no-code-entry, time-boxed (`starts_at`/`ends_at`) item-targeted promos: buy-N-get-M free (same or different item), two items priced as one fixed total, and a percent/fixed/override discount on a specific item, respectively. Computed server-side at checkout via a claimed-quantity pricing pass (BOGO → Combo → Item Discount) so a unit matching more than one active rule is never discounted twice, then stacked underneath the existing Senior/PWD and promo-code discounts.
- `transactions`, `transaction_lines` (each line also carries a resolved `promo_discount_amount`/`applied_promo_label` for cart/receipt display), `payments`
- `audit_log` (actor, action_type, target, before/after, timestamp) — covers voids, refunds, discounts, price overrides, inventory adjustments
- `customer_credit_ledger` (sari-sari utang mode — phase 2, schema reserved now)

## 5. Offline & Sync Strategy
- All writes go to local SQLite first, tagged `pending_sync`
- Background sync attempts on connectivity restore; batched to `/sync`
- Conflict rule (as previously decided): if two devices produce conflicting state for the same resource, the later-timestamped one is auto-cancelled and flagged for manual review — never silently dropped
- Local storage encrypted at rest (SQLCipher or platform-level encryption) since it holds transaction/payment data on a device that can be lost/stolen

## 6. Licensing/Monetization Hooks (architecture only — model undecided)
Build these interfaces now regardless of final model, so no rework is needed once the business decision is made:
- `tenants.license_status` field (active/trial/suspended) checked at app startup and periodically — works for subscription or one-time-with-support-expiry
- Transaction-count metering table (`tenant_id`, `period`, `count`) — populated regardless of model, only *used* for billing if per-transaction is chosen
- Feature-flag gating already exists via `feature_flags` — makes tiered subscription plans (if chosen) a config change, not a rebuild

## 7. Security
- Server-enforced RBAC on every endpoint (never trust client-side role hiding)
- Full audit log for: voids, refunds, post-sale discounts, price overrides, inventory adjustments
- Device pairing + staff PIN/login required per session; auto-logout on idle
- Encrypted local storage (see §5)
- HTTPS-only API, JWT short expiry + refresh token rotation

## 8. Compliance (BIR)
- Sequential, gap-auditable receipt numbering per branch/device, generated server-side to prevent tampering
- Z-reading (end of day) / X-reading (mid-shift) report generation, formatted per BIR requirements
- Senior Citizen/PWD discount rule engine (auto tax recalculation, required receipt annotations)
- Data retention aligned with Data Privacy Act (NPC) requirements — retention policy configurable per tenant, default per legal minimum

## 9. Hosting & Migration Path
- **Phase 0 ($0):** Supabase (Postgres + storage + auth) free tier, or Railway free tier for API + DB; Flutter app side-loaded/TestFlight for pilot testing
- **Phase 1 (first paid client):** upgrade same provider to paid tier — no migration, just billing tier change
- **Phase 2 (scale):** evaluate Azure/AWS only if a client's compliance or scale needs exceed what Supabase/Railway offer — avoid premature migration cost

## 10. Explicitly Deferred (do not build until a real client requires it)
- Full kiosk module (portrait UI, idle-screen flow)
- Multi-branch transfer workflows
- Department/concessionaire mode
- Utang/credit ledger enforcement (schema only, no UI/logic yet)
- Weight/volume and bundle pricing engines (schema supports `pricing_type`, engines built when a grocery/convenience client is signed)
