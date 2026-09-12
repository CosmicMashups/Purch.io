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

## Build sequence (phased delivery)

The system was built in explicit phases, each shipping backend and client changes together and gated behind working tests:

1. **Phase 0** — monorepo scaffold, ASP.NET Core solution bootstrap.
2. **Phase 1** — full domain model, tenant isolation, JWT auth, PIN login, RBAC and tenant-isolation tests, global error handling, offline drift schema on the client.
3. **Phase 2** — onboarding flows (bootstrap, staff/branch/device management), tenant settings, audit log.
4. **Phase 3** — catalog engine: categories/items/unit pricing, modifier groups, weight/volume batching, bundle promo rules, variant matrices, combo/meal builder slots, service-duration pricing, department/concessionaire assignment, credit ledger ("utang") toggle, barcode scanner hardware integration.
5. **Phase 4** — checkout/transaction engine: cart and transaction processing, Senior/PWD discount auto-recalculation, combo/variant customization at checkout, multiple payment method tabs (cash, bank transfer, manual GCash QR), minimal BIR-style receipt numbering, BIR X/Z-reading report generation.
6. **Phase 5** — inventory: stock movement log with full type taxonomy, low-stock dashboard, multi-branch stock transfer, supplier & purchase order management.
7. **Phase 6** — multi-device sync engine: a server-side conflict-resolution ledger (idempotency-key replay protection, later-timestamp-auto-cancel with retroactive re-flagging for out-of-order arrivals) plus a client-side offline write queue and drain coordinator. Deliberately scoped to the ledger/queue infrastructure itself — retrofitting existing feature write-paths onto the queue was flagged as a separate follow-up, not silently assumed done (see `docs/adr/0003`).
8. **Phase 7** — self-service kiosk module (order-preparation only, no payment): a portrait route tree separate from the landscape shell, reusing the checkout engine's combo/variant customization logic. A kiosk pairs without a staff PIN but is structurally excluded from every payment/discount/promo/utang endpoint by role, not by hiding UI — a cashier claims a submitted kiosk order back into the normal, already-tested payment pipeline.
9. **Phase 8** — reporting: a sales dashboard with branch comparison, inventory movement/low-stock reports with a CSV export, a staff performance report — all gated by a new `ScopeType`/`ScopeId` enforcement layer so a branch-scoped manager can't see another branch's numbers even with the same role.
10. **Phase 9** — department/concessionaire mode and utang (credit ledger) checkout enforcement: customer credit accounts with a credit-limit check at the point of sale, a repayment flow, due-date reminders, and a split sales-attribution report between concessionaire departments and general stock.
11. **Phase 10** — on-prem installer packaging: a Docker Compose path and a self-contained Windows Service path for the Local deployment mode, plus a client-side runtime server-address override so a physical device can be pointed at a specific installation's LAN address without a rebuild.
12. **Phase 12** — CI/CD for both deployment modes: a shared Dockerfile used by both Cloud (`render.yaml`) and Local (the installer), and a CI job that publishes the backend self-contained and proves it self-migrates against a schema-less Postgres — simulating the on-prem installer end to end rather than trusting the config by inspection alone.

## Notable outcomes worth highlighting

- Designed and shipped a single pricing/config model that drives six distinct business verticals from one codebase, avoiding per-client forks.
- Built an offline-first sync design with explicit, auditable conflict resolution rather than a naive last-write-wins — and was explicit in the project's own record about the boundary between "the ledger is built" and "every write path uses it," rather than letting that distinction blur.
- Shipped a second, fully independent deployment mode (dedicated on-prem installation per tenant) behind the same codebase and the same `IDeploymentContext` seam used for Cloud — proven by an actual CI job that builds, boots, and writes to a real Postgres instance, not just a docs claim.
- Enforced RBAC at two orthogonal axes — Role (what) and ScopeType/ScopeId (which data) — so a multi-branch reporting surface can't leak another branch's numbers to a scoped manager.
- Structurally excluded an entire class of unattended-terminal risk (kiosk can't pay, discount, or charge to credit) by never granting that role access to the relevant endpoints, rather than relying on client-side UI hiding.
- Treated regulatory compliance (BIR receipts, Senior/PWD discounts, NPC data-privacy registration) as first-class schema concerns from Phase 1 instead of a late bolt-on.
- Used ADRs to make infrastructure and compliance tradeoffs (hosting provider, ORM choice, payment provider, sync engine's actual scope boundary) explicit and revisitable as the build progressed, not just at the start.

## Suggested resume bullets

- Architected a config-driven POS platform (Flutter + ASP.NET Core + PostgreSQL) serving six SME business verticals from a single codebase via a data-driven pricing model, eliminating per-vertical forks.
- Designed and implemented an offline-first, multi-device sync protocol with idempotency-key replay protection and auditable conflict resolution (later-timestamp-auto-cancel with retroactive re-flagging), never silently dropping a conflicting write.
- Built a dual-deployment architecture (shared Cloud vs. dedicated on-prem per tenant) behind one config seam, including a self-contained installer and a CI job that proves the on-prem path actually boots and self-migrates against Postgres.
- Implemented server-enforced RBAC with an orthogonal data-visibility scope layer (Role × ScopeType/ScopeId), encrypted local storage, and a full audit trail (voids/refunds/discounts/overrides) to meet retail security and compliance requirements.
- Shipped a self-service kiosk ordering flow structurally barred from payment/discount/credit paths by role rather than UI convention, handing off to a cashier-claimed checkout for finalization.
- Delivered a phased build (domain model → onboarding → catalog/pricing → checkout → inventory → sync → kiosk → reporting → credit ledger → on-prem packaging → CI/CD) with test coverage for tenant isolation, RBAC, and scope enforcement at each phase.
