# Purch.io — Architecture Essentials
*Quick reference. Full detail in ARCHITECTURE.md.*

## Stack at a Glance
| Layer | Choice | Why |
|---|---|---|
| Client | Flutter (Dart) | Single codebase, consistent UI, strong offline tooling |
| Client state | Riverpod | Less boilerplate for small team |
| Client local DB | drift (SQLite) | Typed offline queue + cached catalog |
| Backend | ASP.NET Core (Minimal API) | Keeps you in C#, free-tier friendly |
| Database | Postgres (Supabase, $0 tier) | Relational core, migrates to paid tier without rework |
| Auth | JWT + role claims | Server-enforced RBAC |
| Hosting (now) | Supabase / Railway free tier | $0 to start, config-only migration to paid later |

## Orientation
- POS / Admin / Inventory / Reports: **Landscape**
- Self-service Kiosk (phase 2): **Portrait**

## Multi-Tenancy
Shared DB, `tenant_id` column on all tenant-scoped tables. Not per-tenant schema — cheaper, adequate at SME scale.

## Config-Driven Vertical Layer
Every merchant has a `business_type` + `feature_flags`. Every item has a `pricing_type`:
`unit | weight_volume | bundle | service | combo | variant_matrix`

This single field decides which item-editor UI and which cart pricing logic activate. **This is the core architectural bet of the whole product** — protect it, don't special-case around it.

## Offline Rule
Local-first write → `pending_sync` flag → batched `/sync` call on reconnect → conflict = later timestamp auto-cancelled + flagged for review (never silently dropped).

## Security Non-Negotiables
- RBAC enforced server-side, always — never trust client role-hiding
- Audit log: voids, refunds, discounts, price overrides, inventory adjustments
- Encrypted local storage (device can be lost/stolen)
- Device pairing + PIN, auto-logout on idle

## BIR Minimum (v1)
- Sequential, server-generated receipt numbers (gap-auditable)
- Everything else (Z/X-reading, Senior/PWD automation) — phase 2, schema-ready now

## MVP Boundary (v1 — no signed client yet, speculative baseline)
**Build:** core catalog (unit pricing only), cart, checkout (cash + 1 digital method), basic stock-in/out, single branch/device, basic sales report, sequential receipts, logo + single theme color.

**Do not build yet:** combo builder, variant matrix, weight/bundle pricing engines, kiosk, multi-branch sync, utang ledger, department mode, licensing enforcement.

**Trigger to re-scope:** the moment a real client is signed — re-derive MVP from *their* vertical, not this speculative list.

## Monetization Hooks (model undecided — architected for all three)
- `tenants.license_status` — works for subscription or one-time+support
- Transaction-count metering table — populated always, billed on only if per-transaction model chosen
- `feature_flags` — makes tiered plans a config change later

## Hosting Migration Path
$0 free tier now → same provider's paid tier at first client → Azure/AWS only if scale/compliance later demands it. No premature migration.
