# Purch.io architecture overview

Status: Living · Last substantive change: 2026-10-16 · Audience: contributors, reviewers

This is the single architecture overview. It merges the former `ARCHITECTURE.md` (original design, 2026-09-18),
`ARCHITECTURE-ESSENTIALS.md` (quick reference, 2026-09-12), the system-analysis executive overview (2026-10-01)
and the web-architecture stub. Where those sources disagreed with each other or with the code, the code-backed
statement is kept here and the disagreement is logged in [history/doc-conflicts.md](../history/doc-conflicts.md).

Reference docs in this folder go deeper: [modules](modules.md), [workflows](workflows.md),
[business rules](business-rules.md), [error flows](error-flows.md), [auth](auth.md) and the
[diagrams](diagrams/README.md). Decisions are in [../adr/](../adr/README.md).

## 1. Context

### Purpose
Purch.io is a multi-tenant, offline-capable, configuration-driven point-of-sale (POS), back-office inventory,
customer credit ledger (*utang*) and self-service kiosk system for Philippine SMEs and retail. Instead of separate
vertical forks for convenience stores, QSR/cafés, apparel boutiques, groceries or appointment-based salons, it uses
one multi-vertical pricing and transaction engine, plus an opt-in Recipe/BOM (bill of materials) ingredient
tracking model. Product framing lives in [../product/](../product/proposal.md).

### Users
1. **Cashier / floor staff**: runs the register, scans items by barcode or touch grid, applies discounts, runs shift
   drawer operations, takes multi-method payments.
2. **Manager / supervisor**: authorizes privileged operations (voids, post-sale refunds, manual drawer opens,
   Senior/PWD discounts), reconciles shifts, monitors X/Z readings, oversees credit limits.
3. **Admin / business owner**: configures tenant settings (branding, theme, fonts, BIR details, barcode enforcement,
   credit ledger retention), manages branches, devices and staff, exports multi-branch sales.
4. **Warehouse officer / storekeeper**: manages ingredient catalogs, supplier purchase orders, receiving against
   open POs, stock movements (damage, spoilage, adjustments) and branch transfers.
5. **Walk-up customer**: uses the portrait self-service kiosk to browse, customize, choose Dine In / Take Out and
   send prep tickets.
6. **Kitchen / counter staff**: watch the unattended Kitchen Display and Order Board and move orders from Queued
   to Preparing, Ready and Picked Up.

### Orientation
- POS / Admin / Inventory / Reports: **landscape**.
- Self-service kiosk (PAGES E1–E4, E6; order preparation only, no payment): **portrait**, a separate route tree from
  the landscape shell.

### Config-driven vertical layer
Every merchant has a `business_type` + `feature_flags`, read by both the backend (validation rules) and the client
(which UI modules render). Every item has a `pricing_type`:
`unit | weight_volume | bundle | service | combo | variant_matrix`.
That single field decides which item-editor UI and which cart pricing logic activate. **This is the core
architectural bet of the product**: protect it, do not special-case around it.

Larger opt-in behaviour changes get their own boolean on `tenants` instead of the generic flags blob, e.g.
`use_separate_inventory_tracking`, which switches a tenant's stock model from direct `items.stock_on_hand` to the
ingredient-level Inventory Item / Recipe model (see §4), additively (off by default, existing tenants unaffected).

## 2. Components and layers

```
[Flutter client] <--offline queue/sync--> [local SQLite (drift)]
[React web client] (online)
        |
        | REST (HTTPS, JWT bearer)
        v
   [ASP.NET Core API] --- [Postgres]
        |
        +--- [Object storage: logos, item images]  (Supabase Storage in Cloud mode, local disk in Local mode)
        +--- [Server-side BIR receipt numbering]
        +--- [Payment integrations: QR Ph via Xendit; bill payment / e-load per ADR 0004]
```

| Layer | Choice | Notes |
|---|---|---|
| Native client | Flutter (Dart), `client/` | Windows desktop, Android tablet, Linux/macOS; one codebase with strong offline tooling; Riverpod (less boilerplate for a small team), `go_router`, drift (typed SQLite) |
| Web client | React 19 + TypeScript, `web/` | Vite, Tailwind CSS 4, react-router 7, TanStack Query 5, zustand; see §7 |
| Backend | ASP.NET Core Minimal APIs on .NET 9, `backend/` | Chosen to stay in C# and to be free-tier friendly. Clean-architecture projects `Purch.Domain`, `Purch.Application`, `Purch.Infrastructure`, `Purch.Api` |
| Persistence | EF Core 9 (`PurchDbContext`) on PostgreSQL | EF Core over Dapper: [ADR 0001](../adr/0001-ef-core-over-dapper.md) |
| Database host | Supabase Postgres (Cloud) or a dedicated Postgres (Local) | See §3 |
| Auth | API-issued JWT + role claims (+ `ScopeType`/`ScopeId`) | In Cloud mode Supabase Auth checks email + password, then the API issues its own tokens; Local mode uses `LocalCredentials`. See §5 and [auth.md](auth.md) |

### Backend
- Clean architecture / DDD layering in the four projects above.
- **API shape:** REST for CRUD (catalog, inventory, staff), plus a dedicated batch sync endpoint (`POST /sync`) that
  accepts a batch of queued offline records and returns conflict resolutions in one round trip, so clients never
  sync item by item.
- **Rate limiting:** ASP.NET Core concurrency and fixed-window limiters on authentication, refresh, password reset
  and shift close.
- Exceptions become RFC 7807 `ProblemDetails`; see [error-flows.md](error-flows.md).

### Flutter client
- **State management:** Riverpod.
- **Local persistence:** drift (typed SQLite) for the offline write queue, cached catalog and cached branding config.
- **Navigation:** role-aware routing (cashier, admin and warehouse officer see different route trees).
- **Theming:** `ThemeData` built at runtime from the tenant's branding config (logo URL, colour hex, font family)
  fetched on sign-in/sync, not hardcoded per build. Design tokens: [../design/tokens.json](../design/tokens.json).
- **Hardware integration points** (`client/lib/core/hardware`): barcode scanner, receipt printer, cash drawer
  (printer-triggered kick), scale and customer-facing display.

## 3. Deployment modes

`DeploymentMode` (Cloud | Local) is resolved once at startup from config via `IDeploymentContext`; endpoints and
application code never branch on it directly. It is a value the developer sets per installation at provisioning
time (`PURCH_DEPLOYMENT_MODE`), not a customer-facing or self-activated toggle. A tenant created in one mode can
never declare itself the other.

- **Cloud mode:** shared multi-tenant backend in a container, backed by Supabase (Postgres + Storage). The repo
  carries two backend host configurations: Vercel (`backend/vercel.json`, `backend/Dockerfile.vercel`,
  [operations/vercel-deployment.md](../operations/vercel-deployment.md)) and Render (`render.yaml`,
  [ADR 0002](../adr/0002-render-over-railway.md); a config-only Render Blueprint). The web client deploys to Cloudflare Pages
  ([operations/cloudflare-deployment.md](../operations/cloudflare-deployment.md)) or Vercel (`web/vercel.json`).
- **Local mode:** one dedicated backend + Postgres per tenant on the tenant's own LAN, provisioned by the installer
  (Docker Compose or a self-contained Windows Service, see [installer/README.md](../../installer/README.md)),
  migrating its own database on first boot. POS terminals on the subnet keep working when internet access is lost
  (only Xendit-dependent QR Ph payments are affected).

Neither mode has a licensing or activation mechanism; see §8.

**Hosting path (from the original plan):** start on free tiers, move to the same providers' paid tiers as revenue
justifies (a billing change, not a migration), and evaluate Azure/AWS only if a client's scale or compliance needs
exceed what those providers offer. Local mode is provisioned per tenant independently of this path.

Backups per mode: [operations/backups.md](../operations/backups.md).

## 4. Data

### Multi-tenancy
Shared database with a `tenant_id` column on every tenant-scoped table, not per-tenant schemas: cheaper and adequate
at SME scale; revisit only if a tenant needs isolation a shared DB cannot give. Isolation is enforced in the data
layer from the JWT claims (`ICurrentTenantProvider`) through EF Core global query filters on `TenantId`. Local mode
additionally puts a tenant on its own database/instance. New tables enable Postgres row level security in their
migration (checked by `RowLevelSecurityCoverageTests`; see the [deploy notes](../operations/deploy-notes/README.md)).

### Core entities (original data model, not exhaustive)
The full entity diagram is [diagrams/database-erd.mmd](diagrams/database-erd.mmd).
- `tenants` (business_type, branding config, feature_flags, license_status)
- `branches`, `devices`
- `users` (role, branch, pin/login). The identity model was later redesigned around accounts and memberships: see
  [auth.md](auth.md).
- `items` (pricing_type: unit/weight/bundle/service/combo/variant, base fields)
- `item_variants` (for the variant-matrix pricing type)
- `item_combo_components` (for the combo pricing type)
- `inventory_movements` (item_id or inventory_item_id, branch_id, type: stock-in / stock-out / consumption / spoiled /
  damaged / for-return / transfer / adjustment / **sale**, quantity, staff_id, timestamp, note). `sale` is logged
  automatically by a completed cashier sale (never a manual entry), kept distinct from a manual `stock-out` and a
  recipe's `consumption` so the log can tell all three apart.
- `inventory_items` (tenant-scoped ingredient/stock record, used only when
  `tenants.use_separate_inventory_tracking` is on: name, base_unit e.g. mL/g/pc, packaging_unit + packaging_size
  e.g. 450 mL per pc, quantity_on_hand always in base_unit, low_stock_threshold, and a `linked_item_id`
  back-reference for the 1:1 fallback record auto-created for a catalog item with no recipe, preserving manual
  Physical Count behaviour)
- `item_recipe_lines` (the BOM: catalog item_id → inventory_item_id, optional quantity_per_order; null means "just
  check availability", set means "auto-consume this much per sale")
- `promo_codes` (cart-level code-entry discount, percentage or fixed, admin-created, cashier-applied at checkout)
- `bogo_promo_rules`, `combo_promo_rules`, `item_discount_promo_rules`: automatic, no-code, time-boxed
  (`starts_at`/`ends_at`) item promos (buy-N-get-M free, two items for one fixed total, and a percent/fixed/override
  discount on one item). Computed server-side at checkout in a claimed-quantity pass (BOGO → Combo → Item Discount)
  so no unit is discounted twice. How they combine with Senior/PWD and promo codes changed later: see
  [business-rules.md](business-rules.md) and the
  [checkout deploy note](../operations/deploy-notes/checkout-and-offline-sales.md).
- `transactions`, `transaction_lines` (each line carries a resolved `promo_discount_amount` / `applied_promo_label`
  for cart and receipt display), `payments`
- `audit_log` (actor, action_type, target, before/after, timestamp): voids, refunds, discounts, price overrides,
  inventory adjustments
- `customer_credit_ledger` (sari-sari utang mode; see [ADR 0006](../adr/0006-npc-registration-tracked-externally.md))

Stock is tenant-wide: `Item.StockOnHand` and `InventoryItem.QuantityOnHand` are single values and branch exists
only on `InventoryMovement` ([ADR 0008](../adr/0008-tenant-wide-stock.md)).

### BIR compliance
- Sequential, gap-auditable receipt numbering per branch/device, generated server-side to prevent tampering.
- Z-reading (end of day) and X-reading (mid-shift) reports.
- Senior Citizen / PWD discount rules (automatic tax recalculation, required receipt annotations).
- Data retention aligned with the Data Privacy Act (NPC), configurable per tenant.

Server-generated receipt numbers shipped in Phase 4; Z/X readings and Senior/PWD automation shipped in Phases 4 and 8
and are best-effort pending real BIR accreditation review
([ADR 0005](../adr/0005-bir-format-pending-accreditation-review.md)).

## 5. Auth and security

The sign-in and device model is specified in [auth.md](auth.md) (people sign in with email and password or unlock a
paired till with a PIN; devices pair once with a short-lived code). The older device-pairing-code + PIN sign-in was
removed (auth.md, step 8).

Non-negotiables carried from the original design:
- RBAC enforced server-side on every endpoint; client role hiding is never trusted. Roles: Admin, Manager, Cashier,
  Warehouse, plus unattended kiosk/display roles.
- `ScopeType`/`ScopeId` restricts *which* branch/department data a role sees, orthogonal to *what actions* it can
  take (a Branch-scoped Manager cannot see another branch's reports).
- Full audit log for voids, refunds, post-sale discounts, price overrides and inventory adjustments.
- Auto-logout on idle; HTTPS-only API; short-lived JWTs (staff/admin 30 minutes, unattended kiosk/display 24 hours
  in `JwtTokenService`) with single-use, hashed refresh tokens; BCrypt-hashed PINs and passwords.
- Kiosk terminals pair without a PIN but are structurally excluded from every payment, discount, promo and utang
  endpoint, by role and not by UI hiding.
- Supervisor-style actions (discounts, price overrides) need a Manager/Admin approver PIN in the cashier session
  ([engineering/roadmap.md](../engineering/roadmap.md), standing decisions).
- The original design called for encrypted local storage (SQLCipher or platform encryption) because devices hold
  transaction data and can be lost. That is **not implemented**: the current state and the compensating controls
  (sealed offline-sale rows, wipe on unpair) are in the
  [security assessment](../operations/security/security-assessment.md).

## 6. Offline and sync

Original rule: local-first write → `pending_sync` flag → batched `POST /sync` on reconnect → on conflict between
devices for the same resource, the later-timestamped write is auto-cancelled and flagged for manual review, never
silently dropped.

What exists:
- Server: the `SyncedRecord` ledger (`POST /sync`, idempotency-key replay protection, later-timestamp auto-cancel
  with retroactive re-flagging for out-of-order arrivals).
- Client: `pending_sync_queue` with `SyncQueueDao`/`SyncCoordinator` draining it on connectivity restore.
- Scope boundary: the generic queue was not retrofitted onto feature write paths
  ([ADR 0003](../adr/0003-offline-sync-stub-boundary.md)). Offline **sales** on Flutter were later added through
  their own queued-sale path to one-call checkout (`ClientSaleId`); see the
  [checkout deploy note](../operations/deploy-notes/checkout-and-offline-sales.md) and the security assessment.
- Web: offline sales are blocked by design (§7).

## 7. Web client

`web/` was extended from a catalog-admin slice into a full client; the backend stays the source of truth. Key
decisions (full record: [ADR 0007](../adr/0007-react-web-client.md) and the migration log
[history/react-migration.md](../history/react-migration.md)):
- Online against the server-side cart pipeline; offline sales are blocked and no TypeScript pricing engine exists.
- Tokens are kept in `localStorage` (XSS trade-off accepted at the time; see the security assessment).
- The tenant is never client-selectable; it derives from the JWT / device pairing.
- Tokens from [../design/tokens.json](../design/tokens.json) feed CSS variables; tenant branding overrides them at
  runtime (`ThemeProvider`). Deployment: [operations/cloudflare-deployment.md](../operations/cloudflare-deployment.md).

## 8. Licensing and monetization

**Not built, by decision.** The original design reserved hooks so a later business model would need no rework:
- `tenants.license_status` (active/trial/suspended), meant to be checked at startup and periodically;
- a transaction-count metering table (`tenant_id`, `period`, `count`), only used for billing under a per-transaction
  model;
- `feature_flags`, which would make tiered plans a config change.

Today `license_status` and the metering table are inert schema-only hooks with no activation flow or gate. Reason: a
locally-run Postgres is fully in the customer's control, so no software-only licensing can prevent cloning it without
disproportionate DRM, and since deployment is hands-on and developer-provisioned there is no enforcement gap to close.
No pricing or tier gating exists anywhere ([engineering/roadmap.md](../engineering/roadmap.md)).

## 9. Scope and modules

The original design deferred several items until a client required them (full kiosk, multi-branch transfers,
department/concessionaire mode, utang enforcement, weight/volume and bundle pricing engines). All of them have since
been built: combo builder, variant matrix, weight/volume and bundle pricing, kiosk, multi-branch sync engine, utang
ledger with checkout enforcement, department/concessionaire mode with split sales attribution, and the on-prem
installer. The build order is in [history/project-case-study.md](../history/project-case-study.md).

Major modules (detail in [modules.md](modules.md); feature rows in
[product/feature-inventory.md](../product/feature-inventory.md)):
1. Authentication and identity
2. Onboarding and tenant management (bootstrap wizard, branches, devices, branding, BIR registration)
3. Unified catalog and pricing (unit, variant matrix, fractional *tingi*/weight/volume, combo slots with
   substitution upcharges, bundles, timed services)
4. Promotions and discounts (promo codes; automatic BOGO, combo bundle and item discounts; Senior Citizen/PWD)
5. POS and cashier engine (device-bound cart, customization, one-call checkout, Cash / Bank Transfer / QR Ph / Utang,
   receipt sequencing, kiosk order claiming)
6. Shift management and cash drawer (open, manual drawer pop log, close with variance)
7. Inventory and recipe/BOM (ingredients, BOM consumption, purchase orders, movement audit, branch transfers)
8. Customer credit ledger (*utang*: schedules, limits, reminders, NPC anonymization)
9. BIR compliance and fiscal reporting (sequential invoices, reset counters, X and Z readings)
10. Unattended displays and kiosk (self-service kiosk, Kitchen Display, Order Status Board)
11. Offline synchronization engine (§6)
