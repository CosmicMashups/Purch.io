# Purch.io

Fit-to-all POS system for Philippine SMEs — config-driven vertical engine (retail, café, grocery, convenience, department store, service establishments), offline-resilient, BIR-compliance-oriented, with per-merchant branding.

Reference vertical for this build: **convenience store**.

## Structure
- `docs/` — specs (`PROPOSAL.md`, `ARCHITECTURE.md`, `ARCHITECTURE-ESSENTIALS.md`, `WORKFLOW.md`, `REQUIREMENTS.md`, `PAGES.md`), `adr/` (architecture decision records), `design/` (Impeccable-generated design tokens)
- `client/` — Flutter app (Riverpod, drift)
- `backend/` — ASP.NET Core Minimal API (Clean layering: Domain → Application → Infrastructure → Api)
- `installer/` — local/on-prem deployment packaging

## Getting started
Backend requires the .NET SDK (not just runtime) — see `docs/adr/` for setup notes once added.
Client: `cd client && flutter pub get && flutter run`.

See `docs/ARCHITECTURE-ESSENTIALS.md` for a quick-reference of the stack and core decisions.
