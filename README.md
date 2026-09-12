# Purch.io

Config-driven POS system for Philippine SMEs — one codebase adapts its pricing engine and UI to retail, café, grocery, convenience, department-store, and service verticals via a data-driven `pricing_type`/`business_type` layer rather than per-vertical forks. Offline-resilient, BIR-compliance-oriented, per-merchant branding, and supports both a shared-cloud deployment and a dedicated on-prem installation per tenant.

Reference vertical for this build: **convenience store**.

All 12 build phases (see `docs/PROJECT-CASE-STUDY.md` for the full writeup) are complete: full domain model through onboarding, catalog/pricing engine, POS checkout, multi-device sync, kiosk self-order, reporting, department/credit-ledger enforcement, on-prem installer packaging, and CI/CD for both deployment modes.

## Structure
- `docs/` — specs (`PROPOSAL.md`, `ARCHITECTURE.md`, `ARCHITECTURE-ESSENTIALS.md`, `WORKFLOW.md`, `REQUIREMENTS.md`, `PAGES.md`), `adr/` (architecture decision records), `design/` (Impeccable-generated design tokens), `PROJECT-CASE-STUDY.md` (portfolio-style writeup of the full build)
- `client/` — Flutter app (Riverpod, drift) — landscape POS/admin/reports shell + a separate portrait kiosk shell
- `backend/` — ASP.NET Core Minimal API (Clean layering: Domain → Application → Infrastructure → Api)
- `installer/` — Local/on-prem deployment packaging (Docker Compose or a self-contained Windows Service — see `installer/README.md`)
- `render.yaml` — Cloud deployment Blueprint for Render (paired with a Supabase Postgres database)
- `.github/workflows/` — CI for both deployment modes: `backend-ci.yml`/`client-ci.yml` (build/format/test), `backend-local-mode-ci.yml` (publishes the backend self-contained and proves it self-migrates against a schema-less Postgres, simulating the on-prem installer), `backend-deploy.yml` (documented placeholder — Render deploys itself from `render.yaml`, not from a GitHub Actions step)

## Deployment modes
Every tenant runs in one of two modes, selected by the `PURCH_DEPLOYMENT_MODE` config value at provisioning time — never a customer-facing toggle (see `IDeploymentContext` and `docs/adr/`):

- **Cloud** — shared multi-tenant Postgres via Supabase, hosted on Render. See `render.yaml`.
- **Local** — a dedicated backend + Postgres on the tenant's own LAN, one Docker/Windows-Service installation per tenant. See `installer/README.md`. Migrates its own database on first boot.

## Getting started

**Backend** (requires the .NET 9 SDK):
```bash
cd backend
dotnet build
dotnet test tests/Purch.UnitTests   # integration tests need Docker — see tests/Purch.IntegrationTests
```
Running the API locally needs `PURCH_DEPLOYMENT_MODE` plus the mode-specific connection string set as environment variables — see `.env.example` at the repo root.

**Client**:
```bash
cd client
flutter pub get
dart run build_runner build --delete-conflicting-outputs   # drift/riverpod codegen, gitignored
flutter run --dart-define=PURCH_API_BASE_URL=<your backend URL>
```
`PURCH_API_BASE_URL` defaults to `https://localhost:5001` if omitted. It can also be overridden at runtime per-device from the app itself (login screen → "Connect to a local server", or Business Settings once logged in) — useful for a Local install where every physical device needs pointing at that installation's own LAN address.

See `docs/ARCHITECTURE-ESSENTIALS.md` for a quick-reference of the stack and core decisions, and `docs/PROJECT-CASE-STUDY.md` for a phase-by-phase account of how the system was actually built.
