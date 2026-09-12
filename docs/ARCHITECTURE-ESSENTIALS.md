# Purch.io — Architecture Essentials
*Quick reference. Full detail in ARCHITECTURE.md. See PROJECT-CASE-STUDY.md for how the full-scope build actually shipped, phase by phase.*

## Stack at a Glance
| Layer | Choice | Why |
|---|---|---|
| Client | Flutter (Dart) | Single codebase, consistent UI, strong offline tooling |
| Client state | Riverpod | Less boilerplate for small team |
| Client local DB | drift (SQLite) | Typed offline queue + cached catalog |
| Backend | ASP.NET Core (Minimal API) | Keeps you in C#, free-tier friendly |
| Database | Postgres (Supabase) | Relational core; shared multi-tenant DB in Cloud mode, dedicated per-tenant instance in Local mode |
| Auth | JWT + role claims (+ ScopeType/ScopeId) | Server-enforced RBAC; scope governs *which* data a role can see, orthogonal to *what actions* it can take |
| Hosting (Cloud mode) | Render, via `render.yaml` | Config-only Blueprint deploy, paired with Supabase |
| Hosting (Local mode) | Docker Compose or a self-contained Windows Service on the tenant's own LAN | See `installer/README.md` — no licensing/activation step, `PURCH_DEPLOYMENT_MODE` is a config value set per installation |

## Orientation
- POS / Admin / Inventory / Reports: **Landscape**
- Self-service Kiosk (E1–E4, E6 — order-preparation only, no payment): **Portrait**, a separate route tree from the landscape shell

## Multi-Tenancy
Shared DB, `tenant_id` column on all tenant-scoped tables. Not per-tenant schema — cheaper, adequate at SME scale. (Local mode additionally isolates a tenant onto its own dedicated database/instance — see Deployment Modes below.)

## Deployment Modes
`DeploymentMode` (Cloud | Local) is resolved once at startup from config via `IDeploymentContext` — endpoints and application-layer code never branch on it directly. Cloud is the shared-Supabase path; Local is one dedicated backend + Postgres per tenant on their own LAN, provisioned by the installer (Docker or Windows Service), migrating its own database on first boot. Neither mode has a licensing/activation mechanism — see `docs/adr/` for why that was deliberately removed rather than built.

## Config-Driven Vertical Layer
Every merchant has a `business_type` + `feature_flags`. Every item has a `pricing_type`:
`unit | weight_volume | bundle | service | combo | variant_matrix`

This single field decides which item-editor UI and which cart pricing logic activate. **This is the core architectural bet of the whole product** — protect it, don't special-case around it.

## Offline Rule
Local-first write → `pending_sync` flag → batched `/sync` call on reconnect → conflict = later timestamp auto-cancelled + flagged for review (never silently dropped). Built in Phase 6 as generic ledger/queue infrastructure; retrofitting existing feature write-paths (POS checkout, inventory, etc.) onto the queue is a flagged, unstarted follow-up — see `docs/adr/0003`.

## Security Non-Negotiables
- RBAC enforced server-side, always — never trust client role-hiding
- `ScopeType`/`ScopeId` further restricts *which* branch/department's data a role can see (e.g. a Branch-scoped Manager can't see another branch's reports even with the same Role)
- Audit log: voids, refunds, discounts, price overrides, inventory adjustments
- Encrypted local storage (device can be lost/stolen)
- Device pairing + PIN, auto-logout on idle
- Kiosk terminals pair without a PIN but are structurally excluded from every payment/discount/promo/utang endpoint — enforced by role, not UI-hiding

## BIR Status (v1)
- Sequential, server-generated receipt numbers (gap-auditable) — shipped Phase 4
- Z/X-reading and Senior/PWD discount automation — shipped Phase 4/8, but flagged best-effort pending real BIR accreditation review (see `docs/adr/0005`)

## Full Scope Shipped
Every item originally listed as "phase 2" or "out of scope until a client requires it" in the source spec docs is built: combo builder, variant matrix, weight/volume + bundle pricing engines, kiosk, multi-branch sync engine, utang/credit ledger with checkout enforcement, department/concessionaire mode with a split sales-attribution report, and the on-prem installer. See `docs/PROJECT-CASE-STUDY.md` for the phase-by-phase build order actually followed.

## Monetization / Licensing
**Explicitly not built, by decision, not oversight.** `tenants.license_status` and a transaction-count metering table remain inert schema-only hooks — no activation flow, no validation gate. `DeploymentMode` (and therefore which tenant runs where) is a value the developer sets per installation at provisioning time, not a customer-facing toggle. See `docs/adr/` for the reasoning: a locally-run Postgres instance is fully in the customer's physical control, so no software-only licensing scheme can prevent cloning it without disproportionate DRM — and since deployment is hands-on and developer-provisioned rather than self-service, there's no enforcement gap to close in the first place.

## Hosting Migration Path
Cloud mode: Render + Supabase now, config-only migration to either provider's paid tier as revenue justifies it, Azure/AWS only if scale/compliance later demands it. Local mode is provisioned per tenant independently of this path — see `installer/README.md`.
