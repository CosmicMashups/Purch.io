# BRAIN.md: where to change what

Read this first when you add or change a feature. It is a task router: for each kind of change it names the files to touch and the traps. For the full map (entities, routes, rules, decisions) read [SECOND-BRAIN.md](SECOND-BRAIN.md). For current project state read [docs/engineering/roadmap.md](docs/engineering/roadmap.md).

Last audited against `main` on 2026-10-16. File paths were checked to exist at that date. If a path is gone, `grep` for the type name.

## 1. Repo map

| Folder | What it is |
|---|---|
| `backend/` | ASP.NET Core (.NET 9) minimal API. Four projects under `backend/src`: `Purch.Domain` (entities, enums), `Purch.Application` (services, DTOs, repository interfaces), `Purch.Infrastructure` (EF Core, repositories, auth, storage, migrations), `Purch.Api` (`Program.cs`, `Endpoints/`). Tests in `backend/tests`. |
| `web/` | React 19 + Vite + Tailwind v4 staff app and kiosk shells. The product UI. `web/src`, tests next to code, Playwright in `web/e2e`. |
| `client/` | Flutter app (older shell). Behind the backend since U1; parity is unit U6. Do not edit it for web features. |
| `shared/` | `pricing-scenarios.json`: golden pricing cases run by the backend, the web engine and Flutter. |
| `installer/` | On-premises LAN install (Docker compose, Windows service). |
| `docs/` | `product/`, `architecture/`, `operations/`, `engineering/` (specs, plans, roadmap), `history/`, `adr/`, `design/`, `manual/`. Index: `docs/README.md`. |

## 2. Commands

- .NET SDK is not on PATH. Use `D:\dotnet-sdk` (see [docs/engineering/roadmap.md](docs/engineering/roadmap.md) Environment notes).
- Backend unit tests: `dotnet test backend/tests/Purch.UnitTests`.
- Backend integration tests: need Docker Desktop (`D:\Programs\DockerDesktop`) running; Testcontainers starts `postgres:16-alpine`. The full suite takes about 20 minutes. Run it once, last, with no edits afterwards.
- New migration, from `backend/`: `dotnet ef migrations add Name --project src/Purch.Infrastructure --startup-project src/Purch.Api`. Design-time needs `PURCH_DEPLOYMENT_MODE=Local`, `LOCAL_DB_CONNECTION_STRING`, `LOCAL_STORAGE_PATH` (dummy values work). Then `dotnet ef migrations has-pending-model-changes`.
- Web (in `web/`): `npm run dev`, `npm test` (vitest), `npm run lint` (oxlint), `npm run build` (`tsc -b` + Vite), `npm run e2e:types`, `npm run e2e` (Playwright, throwaway Postgres, localhost only).
- Screenshots for the manual: `npm run manual:shots` in `web/` (seed in `web/e2e/manual/seed-demo.ts`, shots in `screenshots.manual.ts`, output `docs/manual/images/`, index `INDEX.md`). Needs Docker or a local Postgres on 55432, `D:\dotnet-sdk` on PATH, and `PW_CHANNEL=msedge` if Playwright's Chromium is not installed. `MANUAL_SKIP_SEED=1` reuses the last seed. The seed wipes the throwaway e2e database; credentials go to the gitignored `web/e2e/.seed/`.

## 3. Rules you must not break

1. **Approver PIN before the transaction.** Call `ApproverAuthorizationService.AuthorizeAsync` BEFORE `ExecuteInTransactionAsync`. A rollback discards the failed-attempt counter and the 5-failure lockout never triggers. The approver is branch-scoped.
2. **Load entities inside the unit-of-work lambda.** `EfUnitOfWork` clears the change tracker on an execution-strategy retry. `ExecuteInTransactionAsync` is not re-entrant.
3. **Supervisor actions** (discount, price override, void, refund, variance close, import commit, stock-take apply) run in the cashier's own session plus a Manager/Admin approver PIN every time. Never gate on session role alone. Senior/PWD discount stays Manager/Admin only.
4. **Every new table** needs `ALTER TABLE "X" ENABLE ROW LEVEL SECURITY;` in its migration (`RowLevelSecurityCoverageTests`).
5. **Tenant scope.** Every tenant entity implements `ITenantScoped`; the global query filter in `PurchDbContext` fails closed. Do not add a global `IsDeleted` filter; soft delete is explicit per query.
6. **Stock is tenant-wide**, not per branch (ADR 0008).
7. **Unknown cost is `null`, never 0.** Margin and COGS exclude unknown-cost lines and report how many.
8. **Money.** Cart cap is `TransactionService.MaxCartAmount` (`numeric(12,2)`), enforced in `RecalculateTotalAsync`. VAT is stamped per sale (`VatRateApplied`); never hard-code 0.12 in reporting.
9. **Single-tender sales must keep X/Z-reading totals byte-identical.** `BirReadingService` computes on the fly on purpose.
10. **Client must not match message text.** Errors carry a stable `code` in problem details (`AppException.Code`). Match on `code`.
11. **Payments are idempotent.** Checkout is keyed by `ClientSaleId`; `POST /transactions/cart/payments` takes a `PaymentKey`.
12. **Cashier cannot see cost.** Cost fields are redacted for Cashier; gross-profit and margin reports are Manager/Admin.
13. **Role gating hides, the API enforces.** Web hides what a role cannot use; the endpoint must still check.
14. **Commits and PRs in this repo never carry a Claude co-author trailer.**
15. **UI work:** run `/impeccable`, `/design-taste-frontend` and `/ui-ux-pro-max:ui-ux-pro-max` first. Hand-rolled components in `web/src/components`, Phosphor icons, tokens in `docs/design/tokens.json`, touch targets at least 48px.
16. **No tier or plan gating exists yet.** Do not add it ad hoc; see the monetization hooks in SECOND-BRAIN.md.

## 4. Task recipes

### Add a backend endpoint
1. Request/response DTOs and service in `backend/src/Purch.Application/<Area>/` (interface `IXService` + `XService`, repository interface `IXRepository`).
2. EF repository in `backend/src/Purch.Infrastructure/Repositories/EfXRepository.cs`.
3. Endpoint in `backend/src/Purch.Api/Endpoints/<Area>Endpoints.cs` (`app.MapGet/MapPost("/route", handler)`), with `.RequireAuthorization(policy => policy.RequireRole(...))` or `.RequirePermission(Permissions.X)`. Add `.RequireRateLimiting(...)` for sensitive routes.
4. DI: add `AddScoped` lines in `backend/src/Purch.Api/Program.cs` (flat list, no `AddX` extensions). New endpoint class: also `app.MapXEndpoints();` near line 620.
5. Tests: unit test beside the service; integration test in `backend/tests/Purch.IntegrationTests` (`<Feature>EndpointsTests.cs`), plus a row in `AccessMatrixTests.cs` if roles matter.
6. Add a row to `docs/product/feature-inventory.md`.

### Add or change an entity
1. Entity in `backend/src/Purch.Domain/Entities/` (inherit `TenantScopedEntity`).
2. `DbSet` in `PurchDbContext`; configuration in `Persistence/Configurations/XConfiguration.cs`.
3. Migration (command above), add the RLS line, run `has-pending-model-changes`.
4. Production: migrations auto-apply at cold start but failure is non-fatal. Run them out of band first (`scripts/migrate-production.*`, see deploy notes).

### Add a tenant setting
Field on `Tenant` (Domain), request DTO and `TenantSettingsService`, route in `OnboardingEndpoints.cs` (`PUT /tenant/settings/...`), web section in `web/src/features/business/components/OptionsSection.tsx` (or its sibling), `web/src/features/tenant` types. Omitted fields must keep their value. Audit settings changes.

### Add a permission
Key in `backend/src/Purch.Application/Auth/Permissions.cs` and in `Permissions.For(Role)`; endpoint `.RequirePermission(...)`; web check via `useSession()`. Update `PermissionsTests` and `AccessMatrixTests`. Custom roles (U5) will make `For` data-driven; keep the lookup in one place.

### Add an audit action
Value in `Purch.Domain/Enums/AuditActionType.cs` (append, never reorder); write with `auditLogRepository.Add(...)` inside the same unit of work; shows up in `GET /audit-logs` and the web filter list.

### Add a web page
1. `web/src/features/<name>/` with `types.ts`, `api.ts` (axios via `lib/apiClient.ts`), `queries.ts` (react-query keys + hooks; invalidate `xKeys.all` on mutation). Model: `features/credit`.
2. Validation in `rules.ts`/`schemas.ts` (zod + react-hook-form); dialogs via `components/forms/FormDialog`.
3. Page component: `PageHeader`, `QueryList`/`DataTable`, `EmptyState`, `ErrorState`; errors through `userMessage(e)`, success through `toast`.
4. Route in `web/src/App.tsx` inside the right `RequireTab`; wrap in `<RequireRole allow={['Admin']}/>` when limited.
5. Nav: hub tile in `web/src/features/business/hub.ts` or `web/src/features/inventory/hub.ts` (`managersOnly`); a new top-level tab also needs `web/src/permissions/navPolicy.ts` and `TAB_META` in `web/src/layouts/AppShell/AppShell.tsx`.
6. Offline cache only if the first query-key part is added to `CACHEABLE_ROOTS` in `web/src/offline/db/cachePolicy.ts`. Do not cache anything keyed by customer, staff or sale.
7. Tests next to code (`*.test.tsx`, helpers in `web/src/test/`); Playwright spec in `web/e2e` if it spans screens.

### Add a report
Service in `backend/src/Purch.Application/Reporting/` (read patterns in `RevenueReportService.cs`), repository method in `EfReportingRepository.cs`, route in `ReportingEndpoints.cs`, CSV variant if needed (listing CSVs are Admin-only), web panel in `web/src/features/reports/components/` and the tab in `ReportsPage.tsx`/`ReportPicker`. Read stamped sale columns for tax; sum `Payment` rows for payment type; exclude null cost from margin.

### Change POS pricing or cart behaviour
Server is the reference: `TransactionService.cs` (2,500+ lines; touch with care), `ManualDiscountPricing.cs`, `SplitTenderService.cs`, `VatBreakdownCalculator.cs`, `CartParkingService.cs`. Add a scenario to `shared/pricing-scenarios.json` (also `docs/engineering/specs/u1-golden-cases.json` for Flutter). Web mirror: `web/src/features/pos/pricing/pricingEngine.ts`. State: `web/src/features/pos/posStore.ts`. Check X/Z totals still match.

### Add a lifecycle kind (deactivate, delete, restore)
`backend/src/Purch.Infrastructure/Lifecycle/LifecycleService.cs`, routes in `LifecycleEndpoints.cs`, web `web/src/features/lifecycle/api.ts` (`LifecycleKind`) and `useLifecycle`.

### Inventory, import and stock-take
Services in `backend/src/Purch.Application/Inventory/` (`StockTakeService.cs`, `Import/ImportService.cs`, `Import/ImportPlanner.cs`, `PurchaseRequestService.cs`, `PurchaseOrderService`). Routes in `InventoryEndpoints.cs`. Web: `web/src/features/inventory/`. Stock changes always post `InventoryMovement` rows; never overwrite quantities. Import: preview, resolutions, then commit with an `Idempotency-Key` header.

### Unattended devices (kiosk, kitchen, order board, customer display)
Backend `KioskEndpoints.cs`, `DeviceDisplayEndpoints.cs`, `CustomerDisplayEndpoints.cs`; web `web/src/features/kiosk/` and `web/src/hardware/display/`. Pairing: `DevicePairingService`, `web/src/features/kiosk/RequireDevice.tsx`.

### Hardware (printer, drawer, scale, scanner)
Web only, in `web/src/hardware/` (settings live in browser storage `purch.hardware`). Scale uses Web Serial (Chrome or Edge).

## 5. Where things live (quick finder)

| Looking for | Go to |
|---|---|
| DI registrations | `backend/src/Purch.Api/Program.cs` |
| Route prefixes | `backend/src/Purch.Api/Endpoints/*Endpoints.cs` |
| Tenant filter, DbContext | `backend/src/Purch.Infrastructure/Persistence/PurchDbContext.cs` |
| Problem details, error codes | `Purch.Api/ErrorHandling/GlobalExceptionHandler.cs`, `Purch.Application/Common/Exceptions/` |
| JWT, sign-in, device sessions | `Purch.Infrastructure/Auth/`, `Purch.Application/Auth/` |
| Rate limits | `Purch.Api/RateLimiting/RateLimiterPolicies.cs` |
| Web router | `web/src/App.tsx` |
| Web role to tab policy | `web/src/permissions/navPolicy.ts` |
| Axios client, refresh | `web/src/lib/apiClient.ts` |
| Brand theme at runtime | `web/src/theme/` |
| Manager PIN dialog | `web/src/components/ApproverPinDialog.tsx`, `web/src/features/shared/useApproverPinAction.ts` |
| Feature list with IDs | `docs/product/feature-inventory.md` |
| Gap status | `docs/product/competitor-gap-status.md` |
| Business rules | `docs/architecture/business-rules.md` |
| Decisions | `docs/adr/` |
| Deploy steps | `docs/operations/deploy-notes/` |

## 6. Danger zones

- `backend/src/Purch.Application/Pos/TransactionService.cs`: very large, central to money. Prefer adding to the focused services next to it.
- `backend/src/Purch.Api/Program.cs`: DI, middleware order, health, retention endpoint. Middleware order matters (tenant resolution before authorization).
- Migrations that touch `Transaction`/`TransactionLine`: backfills must set `VatDerived = true` for history.
- Offline (`web/src/offline`): web selling is online-only by design (ADR 0007). Do not add offline selling to web.
- Parked carts use status value 5; Flutter must never receive them.

## 7. Before you say done

Backend unit tests and the affected integration tests pass; `npm run lint`, `npm test` and `npm run build` clean in `web/`; a feature-inventory row added; docs updated if a rule or route changed; the manual chapter updated if a screen changed (`docs/manual/`).
