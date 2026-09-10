# Purch.io — Project Proposal

## 1. Problem Statement
Philippine SMEs (cafés, restaurants, clothing shops, department stores, convenience stores, grocery stores, sari-sari stores) rely on POS software that is either:
- Rigid — built for one business type, forcing irrelevant fields/workflows on others
- Shallow on inventory — tracks "sold/in stock" but not spoilage, damage, returns, or internal consumption
- Weak offline — loses or duplicates transactions when internet drops
- Compliance-painful — BIR accreditation, Z/X-reading, and Senior/PWD discount handling bolted on poorly
- Payment-fragmented — doesn't cleanly handle the real PH mix of cash, cards, GCash, Maya, QR Ph in one flow
- Unbranded — merchants can't apply their own logo/theme/font, so the software looks like the vendor's product, not theirs

## 2. Proposed Solution
**Purch.io**: a single POS core with a **configurable vertical layer**. One engine handles sales, inventory, payments, and reporting; a `pricing_type` and `business_type` configuration determines which UI and pricing logic activate per merchant (combo-meal builder, variant matrix, weight/volume pricing, bundle pricing, service/appointment items, utang/credit ledger, department/concessionaire mode).

Built offline-first for Android/iOS tablets, BIR-compliance-ready from day one, with merchant-level branding (logo, color theme, font).

## 3. Target Market
Philippine SMEs across: restaurants/cafés, clothing/retail shops, department stores, convenience stores, grocery stores, sari-sari stores, and service-based establishments (salons, repair shops) needing appointment-style item handling.

## 4. Competitive Differentiation
| Existing pain point | Purch.io answer |
|---|---|
| One-size-fits-none UI | Config-driven vertical UI from one core engine |
| Shallow inventory | Full movement ledger: stock-in, stock-out, consumption, spoiled, damaged, for-return, transfer |
| BIR compliance friction | Built-in receipt sequencing, Z/X-reading, Senior/PWD discount handling |
| No brand identity | Per-merchant logo, color theme, font |
| Fragile offline | Offline-first queue with conflict resolution |
| Payment silos | Unified payment layer: cash, card, GCash/Maya, QR Ph, bill payment/e-load, utang |

**Positioning statement:** *"One POS core, BIR-ready and offline-capable, that reconfigures itself — not its price tag — for whatever you sell."*

## 5. MVP Scope (Phase 1)
> No signed client as of this proposal — scope below is a speculative baseline using retail/sari-sari as the reference vertical (simplest pricing model, easiest to extend). **Revisit and re-scope immediately once a real client is confirmed** — do not build past this list without one.

**In scope (v1):**
- Core: item catalog, unit-based pricing, cart, checkout, cash + one digital payment method
- Inventory: stock-in, stock-out, basic adjustment
- Branding: logo, single theme color
- Single branch, single device
- Basic sales report (daily total, top items)
- BIR-minimum: sequential receipt numbering

**Explicitly out of scope until a client requires it:**
- Combo-meal builder, variant matrix, weight/volume pricing, bundle pricing, service items
- Multi-branch, multi-device sync, offline queue (build the schema for it, defer the full sync engine)
- Kiosk module
- Utang/credit ledger, department/concessionaire mode
- Full BIR Z/X-reading, Senior/PWD discount automation
- Multi-tenant licensing/entitlement logic (architect for it, don't build the enforcement yet)

## 6. Business Model
Undecided by design — architecture will support subscription, per-transaction, or one-time licensing without rework (see ARCHITECTURE.md §6). **A decision is still needed before Phase 2** — flexibility in the code doesn't remove the need for a real pricing decision before go-to-market.

## 7. Hosting/Cost Plan
- **Now:** $0 free-tier hosting (target: Supabase or Railway free tier for DB + API, Cloudflare Pages/Workers or Render free tier for any web-facing pieces)
- **At first client:** migrate to paid tier of the same providers (minimal migration cost — same platform, higher tier) or move to Azure/AWS if the client's scale or compliance needs demand it
- Preparation now: keep infra-as-config (env vars, no hardcoded free-tier assumptions) so the migration is a config change, not a rebuild

## 8. Risks
- No signed client — highest risk is building features nobody asked for (see MVP scope note above)
- Solo/small team learning C# while shipping a compliance-sensitive product — budget extra time for BIR accreditation paperwork, which is a real bottleneck independent of code
- Name "Purch.io" reads as procurement, not POS — validate before it's on external materials
