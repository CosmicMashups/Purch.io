# Purch.io — Development Case Study

*A portfolio-ready writeup of the system's design and build. Use this as raw material for resume bullets, portfolio pages, or interview talking points — trim to fit the context.*

## One-line summary

Purch.io is a config-driven, offline-resilient point-of-sale (POS) system built for Philippine small and medium businesses — a single codebase that adapts its pricing engine and UI to retail, café, grocery, convenience, department-store, and service verticals through a data-driven configuration layer rather than per-vertical forks.

## Problem

Off-the-shelf POS software for Philippine SMEs is either too generic (built for a single retail pattern, awkward for cafés or service businesses) or too expensive/rigid (enterprise systems requiring per-vertical custom builds). Businesses also need to keep operating during internet outages and eventually meet BIR (Bureau of Internal Revenue) receipt and audit requirements — both easy to bolt on badly and hard to retrofit.

## Core architectural bet

Every catalog item carries a `pricing_type`: `unit | weight_volume | bundle | service | combo | variant_matrix`. That single field determines which item-editor UI renders and which cart pricing logic runs at checkout. Every merchant additionally carries a `business_type` and `feature_flags`. This turns "support a new business vertical" from a code fork into a configuration change — the stated design principle of the project is to protect this field and vertical-specific logic branches off it rather than special-casing around it.

## Stack

| Layer | Choice | Reasoning |
|---|---|---|
| Client | Flutter (Dart) | One codebase across POS terminal / tablet / kiosk form factors, strong offline tooling |
| Client state | Riverpod | Lower boilerplate for a small team |
| Client local DB | drift (SQLite) | Typed offline write queue + cached catalog |
| Backend | ASP.NET Core Minimal API | Clean layered architecture (Domain → Application → Infrastructure → Api) in C# |
| Database | PostgreSQL (Supabase) | Relational core; free tier to start, no rework needed to migrate to paid tier |
| Auth | JWT + server-enforced role claims | RBAC that can't be bypassed by hiding client UI |
| Hosting (Cloud mode) | Render + Supabase, via a `render.yaml` Blueprint | Config-only deploy, deliberately deferred infra cost until revenue justifies a paid tier |
| Hosting (Local mode) | Docker Compose or a self-contained Windows Service on the tenant's own LAN | Dedicated per-tenant data isolation for stores that want on-prem, no shared-cloud dependency |

## Key engineering decisions

- **Multi-tenancy**: shared database with a `tenant_id` column on every tenant-scoped table, rather than per-tenant schemas — cheaper to operate and sufficient at SME scale.
- **Offline-first sync**: local-first writes are flagged `pending_sync` and batched to a `/sync` endpoint on reconnect. Conflicts resolve by "later timestamp wins, loser auto-cancelled and flagged for review" — explicitly never silently dropped, since silent data loss at a cash register is unacceptable.
- **Security non-negotiables baked in from Phase 1**: RBAC is enforced server-side only; an audit log tracks voids, refunds, discounts, price overrides, and inventory adjustments; local storage is encrypted; devices require pairing plus PIN with auto-logout on idle.
- **BIR compliance staged deliberately**: v1 ships sequential, server-generated, gap-auditable receipt numbers; Z/X-reading and Senior/PWD discount automation were scoped for phase 2 but the schema was built to support them from day one, avoiding a compliance retrofit.
- **Monetization deferred without foreclosing options**: a `tenants.license_status` field and a transaction-count metering table are populated from day one regardless of pricing model, so subscription, one-time+support, or per-transaction billing all remain configuration decisions rather than re-architecture.
- **Documented tradeoffs via ADRs**: architecture decisions (EF Core over Dapper, hosting provider choice, offline-sync boundary, bill-payment provider selection, BIR format review status, data-privacy registration tracking) are recorded as individual ADRs rather than left as implicit team knowledge.
- **Role-aware navigation shell**: the client's staff app runs on a single `go_router` `StatefulShellRoute.indexedStack`, with which of its five bottom-nav tabs render driven by the same `StaffRole` claim the backend already enforces — a Cashier's device physically doesn't render the Inventory/Reports/Business tabs, a Warehouse device doesn't render Sell/Reports/Business, and so on. This is presentation-layer convenience only: every underlying action stays independently authorized server-side, so a hidden tab is never the actual security boundary.

## Build sequence (phased delivery)

The system was built in explicit phases, each shipping backend and client changes together and gated behind working tests:

1. **Phase 0** — monorepo scaffold, ASP.NET Core solution bootstrap.
2. **Phase 1** — full domain model, tenant isolation, JWT auth, PIN login, RBAC and tenant-isolation tests, global error handling, offline drift schema on the client.
3. **Phase 2** — onboarding flows (bootstrap, staff/branch/device management), tenant settings, audit log. Later revisited as a guided wizard (business → branch → admin) with a Terms of Service/Privacy Policy acceptance gate, a branded splash screen, and a redesigned login screen — reachable afterward from Business Settings.
4. **Phase 3** — catalog engine: categories/items/unit pricing, modifier groups, weight/volume batching, bundle promo rules, variant matrices, combo/meal builder slots, service-duration pricing, department/concessionaire assignment, credit ledger ("utang") toggle, barcode scanner hardware integration.
5. **Phase 4** — checkout/transaction engine: cart and transaction processing, Senior/PWD discount auto-recalculation, combo/variant customization at checkout, multiple payment method tabs (cash, bank transfer, manual GCash QR), minimal BIR-style receipt numbering, BIR X/Z-reading report generation.
6. **Phase 5** — inventory: stock movement log with full type taxonomy, low-stock dashboard, multi-branch stock transfer, supplier & purchase order management.
7. **Phase 6** — multi-device sync engine: a server-side conflict-resolution ledger (idempotency-key replay protection, later-timestamp-auto-cancel with retroactive re-flagging for out-of-order arrivals) plus a client-side offline write queue and drain coordinator. Deliberately scoped to the ledger/queue infrastructure itself — retrofitting existing feature write-paths onto the queue was flagged as a separate follow-up, not silently assumed done (see `docs/adr/0003`).
8. **Phase 7** — self-service kiosk module (order-preparation only, no payment): a portrait route tree separate from the landscape shell, reusing the checkout engine's combo/variant customization logic. A kiosk pairs without a staff PIN but is structurally excluded from every payment/discount/promo/utang endpoint by role, not by hiding UI — a cashier claims a submitted kiosk order back into the normal, already-tested payment pipeline.
9. **Phase 8** — reporting: a sales dashboard with branch comparison, inventory movement/low-stock reports with a CSV export, a staff performance report — all gated by a new `ScopeType`/`ScopeId` enforcement layer so a branch-scoped manager can't see another branch's numbers even with the same role.
10. **Phase 9** — department/concessionaire mode and utang (credit ledger) checkout enforcement: customer credit accounts with a credit-limit check at the point of sale, a repayment flow, due-date reminders, and a split sales-attribution report between concessionaire departments and general stock.
11. **Phase 10** — on-prem installer packaging: a Docker Compose path and a self-contained Windows Service path for the Local deployment mode, plus a client-side runtime server-address override so a physical device can be pointed at a specific installation's LAN address without a rebuild.
12. **Phase 12** — CI/CD for both deployment modes: a shared Dockerfile used by both Cloud (`render.yaml`) and Local (the installer), and a CI job that publishes the backend self-contained and proves it self-migrates against a schema-less Postgres — simulating the on-prem installer end to end rather than trusting the config by inspection alone.
13. **Phase 13** — ingredient-level inventory and automatic promos, both opt-in and purely additive to every prior phase:
    - **Recipe/BOM inventory decoupling**: a new `InventoryItem` entity (its own unit of measure plus a packaging size, e.g. "450 mL per pc") and an `ItemRecipeLine` link table let a tenant separate what's *sold* (a menu item) from what's *tracked* (its ingredients), gated behind a `Tenant.UseSeparateInventoryTracking` toggle that leaves every existing tenant's behavior untouched. A menu item with no recipe still gets manual Physical Count tracking via an auto-created 1:1 Inventory Item — the same UX as before, just backed by the new model.
    - **Correctness pass on the decoupling**: an early cut left `Item.StockOnHand` still silently decrementing on every sale for recipe-tracked tenants (never replenished once a tenant switched over, so it drifted negative and produced false "Out of Stock" badges) — caught in review and fixed by skipping that path entirely once a tenant opts in, and by making the Cashier's OOS badge trust only the server-computed field instead of re-deriving it from the now-stale one. A second pass batched what had been an N+1 query per catalog item into three queries for the whole listing.
    - **Movement audit trail gap**: a completed sale on a directly-tracked (non-recipe) item changed stock with zero `InventoryMovement` logged at all, unlike every other stock-affecting action in the system. Added a dedicated `MovementType.Sale`, distinct from a manual `StockOut` or a recipe's `Consumption`, so every stock change — automated or manual — now has a typed, auditable reason.
    - **Automatic, no-code promos**: three time-boxed rule types — Buy-1-Take-1 (same or cross-item), a fixed-total combo bundle for two named items, and a percent/fixed/override discount on a specific item — apply themselves at checkout with no cashier action, computed by a standalone, unit-tested `ItemPromoPricingCalculator` that tracks claimed quantity per cart line across a fixed BOGO → Combo → Item-Discount pass order so a unit matching more than one active rule is never discounted twice. Stacks underneath the pre-existing Senior/PWD and promo-code discounts rather than replacing them.
    - **Cart/receipt display audit**: reviewing the promo work surfaced that a transaction line's chosen variant and combo slot picks were tracked but never actually shown anywhere — `TransactionLineDto` only exposed a raw variant ID with no resolved name, and combo selections were resolved server-side but never rendered client-side. Fixed on both the Cashier cart, the printed receipt, and (once flagged as a related gap) the customer-facing Kiosk cart, so every line now shows its variant, combo picks, modifiers, and any applied promo together — not some of them.

## Notable outcomes worth highlighting

- Designed and shipped a single pricing/config model that drives six distinct business verticals from one codebase, avoiding per-client forks.
- Built an offline-first sync design with explicit, auditable conflict resolution rather than a naive last-write-wins — and was explicit in the project's own record about the boundary between "the ledger is built" and "every write path uses it," rather than letting that distinction blur.
- Shipped a second, fully independent deployment mode (dedicated on-prem installation per tenant) behind the same codebase and the same `IDeploymentContext` seam used for Cloud — proven by an actual CI job that builds, boots, and writes to a real Postgres instance, not just a docs claim.
- Enforced RBAC at two orthogonal axes — Role (what) and ScopeType/ScopeId (which data) — so a multi-branch reporting surface can't leak another branch's numbers to a scoped manager.
- Structurally excluded an entire class of unattended-terminal risk (kiosk can't pay, discount, or charge to credit) by never granting that role access to the relevant endpoints, rather than relying on client-side UI hiding.
- Treated regulatory compliance (BIR receipts, Senior/PWD discounts, NPC data-privacy registration) as first-class schema concerns from Phase 1 instead of a late bolt-on.
- Replaced the client's original flat, ungrouped button list with a role-filtered five-tab dashboard shell (Home/Sell/Reports/Inventory/Business) built on `go_router`, so the same login screen produces a visibly different, minimal app for a Cashier than for an Admin without a second client build.
- Used ADRs to make infrastructure and compliance tradeoffs (hosting provider, ORM choice, payment provider, sync engine's actual scope boundary) explicit and revisitable as the build progressed, not just at the start.
- Gated tenant creation on an explicit Terms of Service/Privacy Policy acceptance step, with both documents kept reachable post-setup from Business Settings rather than a one-time, un-revisitable checkbox.
- Decoupled "what's sold" from "what's tracked" for made-to-order verticals (recipe/BOM inventory) as a fully opt-in, additive data model change — zero behavior change for any tenant that doesn't turn it on — rather than a breaking migration of the existing single-item stock model.
- Caught and fixed a self-introduced correctness bug (stock silently drifting negative for recipe-tracked tenants, masking true availability behind a stale field) during the same review pass that shipped the feature, rather than after it reached users.
- Built the automatic-promo pricing engine as an isolated, pure, unit-tested calculator rather than inline cart logic — letting five distinct stacking/overlap scenarios (same-item BOGO, cross-item BOGO, leftover combo units, each discount type, and double-discount prevention) be verified without standing up the full POS engine.

## Suggested resume bullets

- Architected a config-driven POS platform (Flutter + ASP.NET Core + PostgreSQL) serving six SME business verticals from a single codebase via a data-driven pricing model, eliminating per-vertical forks.
- Designed and implemented an offline-first, multi-device sync protocol with idempotency-key replay protection and auditable conflict resolution (later-timestamp-auto-cancel with retroactive re-flagging), never silently dropping a conflicting write.
- Built a dual-deployment architecture (shared Cloud vs. dedicated on-prem per tenant) behind one config seam, including a self-contained installer and a CI job that proves the on-prem path actually boots and self-migrates against Postgres.
- Implemented server-enforced RBAC with an orthogonal data-visibility scope layer (Role × ScopeType/ScopeId), encrypted local storage, and a full audit trail (voids/refunds/discounts/overrides) to meet retail security and compliance requirements.
- Shipped a self-service kiosk ordering flow structurally barred from payment/discount/credit paths by role rather than UI convention, handing off to a cashier-claimed checkout for finalization.
- Delivered a phased build (domain model → onboarding → catalog/pricing → checkout → inventory → sync → kiosk → reporting → credit ledger → on-prem packaging → CI/CD → ingredient inventory & automatic promos) with test coverage for tenant isolation, RBAC, and scope enforcement at each phase.
- Designed and shipped an opt-in ingredient-level inventory model (recipe/BOM, unit-of-measure, and packaging) alongside an automatic, no-code promo engine (BOGO, combo bundles, timed item discounts), both additive to the existing single-item stock and code-based promo systems with zero behavior change for tenants that don't opt in.
