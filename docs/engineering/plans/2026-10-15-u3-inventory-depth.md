# U3 Inventory Depth Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add inventory valuation, inventory-side COGS, CSV export/import of items/ingredients/stock, and stock-take sessions with variance.

**Architecture:** Three slices on the existing Application/Api/Infrastructure layering. Valuation and COGS are read-only report services. Stock-take and import add tenant tables (RLS) and services that post `InventoryMovement` adjustments through `IInventoryMovementService` internals. Web adds Reports tabs, `/inventory/stock-takes` pages and an import wizard.

**Tech Stack:** .NET minimal APIs + EF Core/Postgres (backend/src), xUnit + Docker integration tests, React + vitest + Playwright (web/src).

**Spec:** `docs/superpowers/specs/2026-10-15-u3-inventory-depth-design.md`

## Global Constraints
- Branch `feat/u3-inventory-depth`; no push, no PR, no Co-Authored-By or Claude/Anthropic text in commits (tell every subagent).
- Stock is tenant-wide; no branch dimension in valuation, count or import.
- Variance = `counted - expected_at_count`; applied as signed `Adjustment` delta; never overwrite stock.
- Unknown cost is never 0: excluded from totals, counted and listed.
- Cost fields hidden for any actor without `ICurrentActorProvider.CanSeeCosts`; Cashier gets 403 on valuation/COGS/stock-take/import/export.
- Apply and import commit: `IApproverAuthorizationService.AuthorizeAsync(pin)` BEFORE `ExecuteInTransactionAsync`; load entities INSIDE the lambda; approver id into `AuditLog.ApprovedByUserId`.
- Every new table: `migrationBuilder.Sql("ALTER TABLE \"<Table>\" ENABLE ROW LEVEL SECURITY;")` in the migration.
- Import caps 5,000 rows / 5 MB; UTF-8 BOM tolerated; blank cell = unchanged; `CLEAR` clears a cost; `0` is a real zero.
- Web: hand-rolled components, Phosphor icons, >= 48px targets, tokens in `docs/design/tokens.json`; run `/impeccable`, `/design-taste-frontend`, `/ui-ux-pro-max:ui-ux-pro-max` before UI tasks. Role gating hides, not disables.
- .NET SDK `D:\dotnet-sdk`; `dotnet-ef` needs `PURCH_DEPLOYMENT_MODE=Local`, `LOCAL_DB_CONNECTION_STRING`, `LOCAL_STORAGE_PATH` dummies. Full integration suite (~20 min, Docker Desktop) once, last.

## Review Focus
- Sale lands between a line's count and apply: no phantom variance (Task 5).
- Same name differing by case/space/symbol/accent (`"Café  & Co"` vs `"cafe and co"`) groups or auto-links, not duplicates (Task 8).
- Barcode with leading zeros or scientific-notation mangling (`0012345` -> `1.2E+5`) is not silently matched/created (Task 8).
- Two managers commit the same batch, or commit after an item changed: second is a no-op / whole batch aborts (Task 9).
- Wrong or locked approver PIN leaves stock untouched and increments lockout counter (Tasks 5, 9).
- CSV cell starting `=`,`+`,`-`,`@` on export is neutralized (Task 7).
- Item with null `CostPrice` and negative on-hand do not distort totals (Task 1).

---

### Task 1: Inventory valuation service + endpoint
**Files:**
- Create: `backend/src/Purch.Application/Reporting/InventoryValuationDto.cs`, `IInventoryValuationService.cs`, `InventoryValuationService.cs`
- Modify: `backend/src/Purch.Api/Endpoints/ReportingEndpoints.cs` (route), `backend/src/Purch.Api/Program.cs` (DI)
- Test: `backend/tests/Purch.UnitTests/Reporting/InventoryValuationServiceTests.cs`, `backend/tests/Purch.IntegrationTests/Reporting/InventoryValuationEndpointTests.cs`

**Interfaces:**
- Produces: `Task<InventoryValuationDto> GetAsync(InventoryValuationQuery q, CancellationToken ct)`; `InventoryValuationQuery(Guid? CategoryId, string Type /*item|ingredient|all*/)`; `InventoryValuationDto(IReadOnlyList<ValuationGroupDto> Groups, decimal TotalValue, int ValuedCount, int UnknownCostCount, int NegativeStockCount)`; `ValuationRowDto(Guid Id, string Kind, string Name, string? Sku, string? Category, decimal OnHand, decimal? UnitCost, decimal? Value, bool Flagged)`.
- Route: `GET /reports/inventory/valuation`, Admin/Manager/Warehouse.

- [ ] **Step 1:** Write failing unit tests: null cost row excluded from `TotalValue` and counted in `UnknownCostCount`; negative on-hand valued 0 and counted in `NegativeStockCount`; inactive included, soft-deleted excluded; item uses `CostPrice`, ingredient uses `UnitCost`; group subtotal = sum of row values.
- [ ] **Step 2:** Run `dotnet test --filter InventoryValuationServiceTests` (fail).
- [ ] **Step 3:** Implement service reading items + inventory items via existing repositories (pattern: `InventoryReportService`); map route with `RequireRole(Admin, Manager, Warehouse)`.
- [ ] **Step 4:** Integration test: Cashier 403; Manager 200 with expected total; Warehouse 200.
- [ ] **Step 5:** Run both test classes (pass); commit `feat(inventory): valuation report endpoint`.

### Task 2: Inventory COGS service + endpoint
**Files:**
- Create: `Purch.Application/Reporting/InventoryCogsDto.cs`, `IInventoryCogsService.cs`, `InventoryCogsService.cs`
- Modify: `ReportingEndpoints.cs`, `Program.cs`; reuse `ReportRange` and the `RevenueReportService` refund/void netting helper
- Test: `Purch.UnitTests/Reporting/InventoryCogsServiceTests.cs`, integration `InventoryCogsEndpointTests.cs`

**Interfaces:**
- Produces: `Task<InventoryCogsDto> GetAsync(DateOnly from, DateOnly to, string groupBy /*item|category|day*/, CancellationToken ct)`; `InventoryCogsDto(IReadOnlyList<CogsGroupDto> Groups, decimal Cogs, decimal KnownCostRevenue, decimal CoveragePercent, int ExcludedLineCount)`.
- Route: `GET /reports/inventory/cogs`, same roles, >366-day range rejected like U4.

- [ ] **Step 1:** Failing tests: `qty x UnitCost` summed; null-cost line excluded and counted, coverage % correct; refunded qty subtracted; voided/parked-expired excluded; open items (no cost) excluded.
- [ ] **Step 2:** Run (fail). **Step 3:** Implement. **Step 4:** Integration: Cashier 403, range limit 400. **Step 5:** Pass; commit `feat(inventory): COGS report endpoint`.

### Task 3: Web valuation + COGS tabs and hub tile
**Files:**
- Modify: `web/src/features/reports/ReportsPage.tsx` (TABS += `valuation`, `cogs`; `valuation.usesRange=false`), `reports/api.ts`, `reports/queries.ts`, `reports/types.ts`, `web/src/features/inventory/hub.ts` (Stock group tile `valuation` to `/reports?tab=valuation`, managersOnly)
- Create: `web/src/features/reports/components/ValuationPanel.tsx`, `CogsPanel.tsx`
- Test: `reports/components/ValuationPanel.test.tsx`, `CogsPanel.test.tsx`, `hub.test.ts`

**Interfaces:** Consumes Task 1/2 DTOs. Produces `reportsApi.inventoryValuation(params)`, `reportsApi.inventoryCogs(params)`; CSV via `toCsv` in `reports/csv.ts`.

- [ ] **Step 1:** Failing tests: unknown-cost banner shows count; totals render; Cashier does not see tab; CSV export contains header and guards formulas.
- [ ] **Step 2:** Run `npx vitest run src/features/reports` (fail). **Step 3:** Implement panels with caveat text ("unknown cost excluded", "recipe use not included in COGS"). **Step 4:** Pass; commit `feat(web): valuation and COGS reports`.

### Task 4: Generalize approver PIN hook
**Files:**
- Create: `web/src/features/shared/useApproverPinAction.ts` (cart-agnostic); keep `pos/useApproverGatedAction.ts` delegating to it
- Test: `useApproverPinAction.test.tsx`, existing pos tests must stay green

**Interfaces:** Produces `useApproverPinAction<T>({ send: (pin?: string) => Promise<T>, onDone?: (r: T) => void })` returning `{ run(title: string), dialogProps }` driving `components/ApproverPinDialog.tsx`.

- [ ] **Step 1:** Failing test: first call without PIN prompts; wrong PIN shows `isError`; success calls `onDone`. **Step 2-4:** Implement by extracting from `useApproverGatedAction.ts` without cache coupling; run `npx vitest run src/features/pos src/features/shared`. Commit `refactor(web): cart-agnostic approver pin action`.

### Task 5: Stock-take domain, repository, service, endpoints
**Files:**
- Create: `Purch.Domain/Entities/StockTake.cs`, `StockTakeLine.cs`, `Purch.Domain/Enums/StockTakeStatus.cs`, `StockTakeLineState.cs`; `Purch.Infrastructure/Persistence/Configurations/StockTakeConfiguration.cs`, `StockTakeLineConfiguration.cs`; migration `AddStockTakes`; `Purch.Application/Inventory/IStockTakeRepository.cs`, `IStockTakeService.cs`, `StockTakeDto.cs`, `StockTakeService.cs`; `Ef` repository beside `EfInventoryMovementRepository`
- Modify: `InventoryEndpoints.cs`, `Program.cs`, `PurchDbContext` DbSets
- Test: `Purch.UnitTests/Inventory/StockTakeServiceTests.cs`, `Purch.IntegrationTests/Inventory/StockTakeEndpointTests.cs`

**Interfaces:**
- Produces on `IStockTakeService`: `CreateAsync(CreateStockTakeRequest)`, `GetAsync(Guid)`, `ListAsync()`, `CountLineAsync(Guid takeId, Guid lineId, decimal? counted, bool skip)`, `StartAsync(Guid)`, `ToReviewAsync(Guid)`, `ApplyAsync(Guid, string? approverPin)`, `CancelAsync(Guid)`. `StockTakeLineDto` omits `ExpectedQty` while `Blind && Status==Counting`; omits cost when `!CanSeeCosts`.
- Routes: `/stock-takes...` in the `inventoryManager` group; `/apply` additionally requires Admin/Manager approver via PIN.
- Consumes: `IApproverAuthorizationService`, `InventoryMovementService` posting path (`MovementType.Adjustment`, signed qty).

- [ ] **Step 1:** Failing unit tests: counting snapshots `ExpectedQty` at entry; recount re-snapshots; sale after count then apply posts `counted - expected` only (stock ends = expected + sale delta + variance); uncounted/skipped post nothing; zero counted lines blocks apply; second apply returns first result; blind hides expected only in Counting; scope Categories materializes only those lines.
- [ ] **Step 2:** Run (fail). **Step 3:** Implement entities/config/migration (RLS SQL), service (PIN before transaction, entities loaded inside lambda, audit entry listing uncounted lines).
- [ ] **Step 4:** Integration tests: Cashier 403; wrong PIN leaves stock unchanged and increments device lockout counter; RLS coverage test passes; overlapping takes allowed.
- [ ] **Step 5:** Generate migration with dotnet-ef (env vars above); commit `feat(inventory): stock-take sessions`.

### Task 6: Stock-take web pages
**Files:**
- Create: `web/src/features/inventory/pages/StockTakesPage.tsx`, `StockTakeDetailPage.tsx`, `components/StockTakeCountList.tsx`
- Modify: `App.tsx` (routes `/inventory/stock-takes`, `/inventory/stock-takes/:id`), `inventory/api.ts`, `queries.ts`, `types.ts`, `hub.ts` (Stock tile)
- Test: `StockTakesPage.test.tsx`, `StockTakeDetailPage.test.tsx`

**Interfaces:** Consumes Task 5 DTOs and Task 4 hook; produces `inventoryApi.stockTakes.*`.

- [ ] **Step 1:** Failing tests: blind mode hides expected column while counting; review lists uncounted prominently and variance with value impact only for cost-visible roles; apply opens `ApproverPinDialog`; wrong PIN shows error; Warehouse sees no Apply button.
- [ ] **Step 2-4:** Implement (>=48px numeric entry rows); `npx vitest run src/features/inventory`; commit `feat(web): stock-take pages`.

### Task 7: CSV export endpoints + web buttons
**Files:**
- Create: `Purch.Application/Inventory/InventoryExportService.cs`, `IInventoryExportService.cs`
- Modify: `InventoryEndpoints.cs` (`GET /inventory/export/items.csv|ingredients.csv|stock.csv`), `Program.cs`; web `IngredientsPage.tsx`, `catalog/pages/ItemListPage.tsx` (Export buttons using `downloadTextFile`)
- Test: `InventoryExportServiceTests.cs`, integration export tests, `ItemListPage` test

**Interfaces:** Produces columns `type,sku,barcode,name,category,price,cost,reorder_threshold,unit,active` (+ read-only `id,current_stock,updated_at`); cost column omitted when `!CanSeeCosts`; reuse `ReportCsv` field escaping (formula guard).

- [ ] **Step 1:** Failing tests: formula-leading cell prefixed; cost column absent for Cashier-equivalent actor; BOM present; header set matches import schema constant `ImportColumns.Catalog`.
- [ ] **Step 2-4:** Implement; pass; commit `feat(inventory): CSV export`.

### Task 8: ImportBatch table, parser, normalizer, matcher (no endpoints)
**Files:**
- Create: `Purch.Domain/Entities/ImportBatch.cs`, config, migration `AddImportBatches` (RLS, unique `(TenantId, IdempotencyKey)`), `Purch.Application/Inventory/Import/CsvImportParser.cs`, `NameKey.cs`, `ImportPlanner.cs`, `ImportColumns.cs`
- Test: `Purch.UnitTests/Inventory/Import/CsvImportParserTests.cs`, `NameKeyTests.cs`, `ImportPlannerTests.cs`

**Interfaces:**
- Produces: `static string NameKey.Of(string s)` (trim, collapse spaces, case-fold, strip diacritics/punctuation, `&`->`and`); `ParsedImport CsvImportParser.Parse(Stream, ImportKind)` throwing `ImportFormatException` for >5,000 rows / >5 MB / bad header; `ImportPlan ImportPlanner.Plan(ParsedImport, ExistingCatalog)` yielding `WillCreate`, `NeedsDecision` (nearest matches, edit distance <= 2), `WillUpdate` (before/after), `RowErrors`.

- [ ] **Step 1:** Failing tests: `"Café  & Co"` and `"cafe and co"` share a key; `"Bevrages"` near-matches `"Beverages"` -> NeedsDecision; SKU match before barcode; barcode `0012345` kept as text and `1.2E+5` rejected as row error; duplicate SKU in file and SKU collision are row errors; blank = unchanged, `CLEAR` sets clear flag, `0` stays 0; BOM tolerated; 5,001 rows rejected.
- [ ] **Step 2-4:** Implement, pass, generate migration; commit `feat(inventory): import parser and planner`.

### Task 9: Import service + endpoints (upload, resolutions, commit)
**Files:**
- Create: `Purch.Application/Inventory/Import/IImportService.cs`, `ImportService.cs`, `ImportDto.cs`
- Modify: `InventoryEndpoints.cs` (`POST /inventory/imports` multipart, `GET /inventory/imports/{id}`, `PUT .../resolutions`, `POST .../commit`), `Program.cs`
- Test: `ImportServiceTests.cs`, integration `InventoryImportEndpointTests.cs`

**Interfaces:**
- Produces: `UploadAsync(Stream, ImportKind, string idempotencyKey)`, `SaveResolutionsAsync(Guid, IReadOnlyList<ImportResolution>)`, `CommitAsync(Guid, string? approverPin)` returning `ImportResultDto`; `ImportResolution(string Key, string Choice /*link|create|rename*/, Guid? TargetId, string? NewName)`.
- Consumes: Task 8 planner; movement posting for opening stock (`StockIn`) and stock deltas (`Adjustment` = `counted - current` at commit).

- [ ] **Step 1:** Failing tests: commit blocked until all decisions made and creations confirmed; re-plan inside transaction differing from preview aborts, nothing applied, decisions kept; double commit returns first result; same idempotency key returns existing batch; expired (>24h) batch rejected; created categories/items listed in audit; stock rows post delta not overwrite; wrong PIN leaves nothing changed and bumps lockout.
- [ ] **Step 2-4:** Implement (PIN before transaction; reload inside lambda); integration incl. Cashier 403; commit `feat(inventory): CSV import with preview and commit`.

### Task 10: Import web wizard
**Files:**
- Create: `web/src/features/inventory/pages/ImportPage.tsx`, `components/ImportReview.tsx`, `CsvFileField.tsx`
- Modify: `App.tsx` (`/inventory/import`), `inventory/api.ts`, `queries.ts`, `hub.ts`
- Test: `ImportPage.test.tsx`

**Interfaces:** Consumes Task 9 API and Task 4 hook; produces three-section review (Will create / Needs decision / Will update).

- [ ] **Step 1:** Failing tests: commit disabled until every decision is resolved and creations ticked; near match offers link/create/rename; stale-abort message highlights changed rows and keeps choices; row errors listed with line numbers; Warehouse can upload but Commit hidden.
- [ ] **Step 2-4:** Implement; `npx vitest run src/features/inventory`; commit `feat(web): inventory import wizard`.

### Task 11: E2E, docs, full verification
**Files:** Create `web/e2e/inventory-depth.e2e.ts`; modify `docs/system-analysis/10-feature-inventory.md` (INV-17..), create `docs/DEPLOY-NOTE-u3-inventory-depth.md` (two migrations, RLS, no Flutter change), update `P1-roadmap-status.md`.

- [ ] **Step 1:** E2E: stock-take count -> review -> apply with PIN; import upload -> resolve -> commit.
- [ ] **Step 2:** Run web build, lint, vitest, Playwright (expect only the 8 known failures), then full backend suite once, no edits afterwards.
- [ ] **Step 3:** Commit `docs(u3): feature inventory and deploy note`.
