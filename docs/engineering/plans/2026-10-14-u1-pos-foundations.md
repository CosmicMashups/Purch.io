# U1 POS Foundations Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add parked carts, split tender, Manager/Admin manual discounts and price override, open items, item cost snapshots and stamped VAT breakdown to the Purch.io POS (backend + web).

**Architecture:** Extend the existing server-owned cart (`TransactionService`, `Transaction`/`TransactionLine`/`Payment`) rather than adding a second cart model. New behaviour goes in focused new services (`CartParkingService`, `ManualDiscountService`, `SplitTenderService`, `VatBreakdownCalculator`) called from `TransactionService`, which is already 2,193 lines. Web adds API/query hooks and UI in `web/src/features/pos`.

**Tech Stack:** .NET 9 Minimal API, EF Core + Postgres (Testcontainers integration tests), React + Vite + Tailwind v4, react-query, react-hook-form + zod, vitest, Playwright.

**Spec:** `docs/superpowers/specs/2026-10-14-u1-pos-foundations-design.md`

## Global Constraints

- .NET SDK is at `D:\dotnet-sdk` (no system SDK); add migrations with `dotnet ef migrations add <Name> --project src/Purch.Infrastructure --startup-project src/Purch.Api` from `backend/`; run `dotnet ef migrations has-pending-model-changes` after.
- Every NEW table needs `ALTER TABLE "X" ENABLE ROW LEVEL SECURITY;` in its migration (`RowLevelSecurityCoverageTests`). U1 adds no new tables; keep it that way.
- VAT rate in force is the constant `0.12m`; stamp `VatRateApplied` on each sale, do not hard-code 0.12 in new reporting code.
- Manual discount / price override: `Admin`, `Manager` only (`posSupervisor`); cashiers never. Reason code required; audit with `AuditActionType.DiscountOverride` / `PriceOverride`.
- Manual discount is exclusive with Senior/PWD (BIR wins) and never stacks with item promos or promo codes.
- Split tender: N payments, one sale, one receipt number; only a Cash part may overpay; at most one `UtangCredit` part; idempotency stays on `ClientSaleId`.
- Unknown cost is `null`, never 0; margin excludes unknown-cost lines.
- No global `IsDeleted` query filter exists; do not add one. No pricing/tier gating.
- Single-tender sales must produce byte-identical X/Z-reading totals to today.
- No Flutter changes in U1 (U6). Server pricing is the reference; add golden-case fixtures so U6 can mirror them.
- Web UI work: run `/impeccable`, `/design-taste-frontend`, `/ui-ux-pro-max:ui-ux-pro-max` before building a screen; tokens in `docs/design/tokens.json` (variance 6, motion 4, density 3); touch targets >= 48px; hand-rolled components in `web/src/components`, Phosphor icons.
- Commit messages end with `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`.

## Review Focus

- Resume of a parked cart whose items were repriced, deleted or out of stock: totals recomputed, changed lines reported, never silently stale.
- Two registers resuming the same parked cart at once: exactly one wins, the other gets 409.
- Split payment where parts sum to total ± 0.01, overpaid non-cash part, or zero/negative part: rejected with 400, no partial `Payment` rows persisted.
- Refund of a split sale that included Utang: ledger balance and each tender part reversed exactly once; Z-reading cash excludes non-cash parts.
- Manual discount then Senior/PWD applied (or the reverse): one discount remains, totals/VAT exempt correct, audit shows the removal.
- Cart with open item only (no `ItemId`): checkout, receipt, refund and stock decrement do not throw on the missing item.
- Parked cart past TTL swept while its device is mid-resume: no double void, no lost lines.

---

## File Structure

| File | Responsibility |
| --- | --- |
| `Purch.Domain/Entities/Item.cs`, `InventoryItem.cs`, `TransactionLine.cs`, `Transaction.cs`, `Payment.cs`, `Tenant.cs` | new columns |
| `Purch.Domain/Enums/TransactionStatus.cs`, `AuditActionType.cs` | `Parked`; `CartParked/CartResumed/CartExpired/OpenItemAdded` |
| `Purch.Application/Auth/Permissions.cs` (new) | permission keys + built-in role map |
| `Purch.Application/Pos/VatBreakdownCalculator.cs` (new) | pure VAT split |
| `Purch.Application/Pos/CartParkingService.cs` (new) | park / list / resume / discard / expire |
| `Purch.Application/Pos/ManualDiscountService.cs` (new) | line/cart discount, override, open item |
| `Purch.Application/Pos/SplitTenderService.cs` (new) | validate + build N `Payment`s |
| `Purch.Api/Endpoints/PosEndpoints.cs` | new routes |
| `Purch.Infrastructure/Retention/RetentionSweeper.cs` | expire parked carts |
| `web/src/features/pos/{api,queries,types}.ts`, `components/*`, `PaymentPage.tsx`, `SellPage.tsx` | web |

---

### Task 1: Schema additions (cost, discount, park, VAT, split)

**Files:**
- Modify: `backend/src/Purch.Domain/Entities/Item.cs`, `InventoryItem.cs`, `TransactionLine.cs`, `Transaction.cs`, `Payment.cs`, `Tenant.cs`, `Purch.Domain/Enums/TransactionStatus.cs`, `AuditActionType.cs`
- Modify: `Purch.Infrastructure/Persistence/Configurations/{Item,TransactionLine,Transaction,Payment}Configuration.cs` (precision `numeric(18,2)`)
- Create: migration `AddU1PosFoundations`
- Test: `backend/tests/Purch.IntegrationTests/U1SchemaTests.cs`

**Interfaces:**
- Produces: `Item.CostPrice decimal?`; `InventoryItem.UnitCost decimal?`; `TransactionLine.{UnitCost decimal?, ManualDiscountAmount decimal (0), PriceOverrideFrom decimal?, IsOpenItem bool, OpenItemName string?, Note string?}` (`TransactionLine.ItemId` becomes `Guid?` so open items have no item; update every `line.ItemId` consumer to handle null); `Transaction.{ParkedLabel string?, ParkedAt DateTimeOffset?, ParkedByUserId Guid?, CustomerId Guid?, ManualDiscountAmount decimal, ManualDiscountReason string?, VatableSales/VatAmount/VatExemptSales/ZeroRatedSales decimal, VatRateApplied decimal?, VatDerived bool}`; `TransactionStatus.Parked` appended last (value 5, do NOT insert: the Open filter is `Status = 0`); `Payment.{Sequence int, Reference string?}`; `Tenant.{ParkedCartTtlHours int=24, OpenItemEnabled bool=true, UpdateCostOnReceiving bool=false}`; `AuditActionType.{CartParked, CartResumed, CartExpired, OpenItemAdded}` appended.

- [ ] **Step 1: Write failing test `U1SchemaTests.NewColumnsRoundTrip`** that creates a Transaction with `Status=Parked`, a line with `UnitCost=12.5m`, a second Payment `Sequence=1`, saves via `PurchApiFactory` DbContext and re-reads all new fields.
- [ ] **Step 2: Run** `dotnet test --filter U1SchemaTests` (use `D:\dotnet-sdk`). Expected: compile FAIL.
- [ ] **Step 3: Add the properties/enum members above** (append enum values; never renumber).
- [ ] **Step 4: Add migration**; in `Up` also add the unique index `(TransactionId, Sequence)` on Payments and relax any existing one-payment-per-transaction constraint found in `PaymentConfiguration`. Confirm `IX_Transactions_OneOpenCartPerDevice` is unchanged in the snapshot.
- [ ] **Step 5: Run** `U1SchemaTests` and `OpenPerDeviceAndMigrationTests`. Expected: PASS.
- [ ] **Step 6: Commit** `feat(pos): schema for parked carts, split tender, cost and VAT stamping`.

### Task 2: Permission shim

**Files:**
- Create: `backend/src/Purch.Application/Auth/Permissions.cs`, `backend/src/Purch.Api/Auth/PermissionPolicyExtensions.cs`
- Modify: `Program.cs` (register policies)
- Test: extend `AccessMatrixTests.cs`

**Interfaces:**
- Produces: `static class Permissions { const string CartPark="cart.park", DiscountApply="discount.apply", PriceOverride="price.override", OpenItemAdd="openitem.add", SplitPay="payment.split"; static IReadOnlySet<string> For(Role role); }` and `RouteHandlerBuilder RequirePermission(this RouteHandlerBuilder b, string key)`.
- Mapping: Admin/Manager all; Cashier = `CartPark, OpenItemAdd, SplitPay`; Warehouse none.

- [ ] **Step 1: Failing unit tests in `Purch.UnitTests/PermissionsTests.cs`:** `For(Role.Cashier)` contains `CartPark, OpenItemAdd, SplitPay` and not `DiscountApply, PriceOverride`; `For(Role.Warehouse)` is empty; `For(Role.Manager)` contains every key. Route-level 403 checks are added to `AccessMatrixTests` in Tasks 5-7 as each route lands.
- [ ] **Step 2: Run, expect FAIL** (`Permissions` undefined).
- [ ] **Step 3: Implement** `Permissions` and `RequirePermission` as `RequireAuthorization(p => p.RequireRole(roles whose set contains key))`; so U5 later swaps the body for claim checks without touching callers.
- [ ] **Step 4: Run unit + access-matrix tests. Expect PASS.** Commit `feat(auth): permission key shim`.

### Task 3: VAT breakdown stamping + backfill

**Files:**
- Create: `Purch.Application/Pos/VatBreakdownCalculator.cs`; migration data step `BackfillTransactionVat`
- Modify: `TransactionService.cs` (in `RecordPaymentCoreAsync`, before receipt issue ~L1087; and `CheckoutCoreAsync` offline path), `BirReadingService.cs` read stamped values when `VatDerived=false`
- Test: `Purch.UnitTests/VatBreakdownCalculatorTests.cs`, `Purch.IntegrationTests/VatStampingTests.cs`

**Interfaces:**
- Produces: `record VatBreakdown(decimal VatableSales, decimal VatAmount, decimal VatExemptSales, decimal ZeroRatedSales, decimal Rate)`; `static VatBreakdown VatBreakdownCalculator.Compute(decimal netPayable, decimal vatExemptAmount, decimal rate = 0.12m)` where `VatableSales = round(netPayable/(1+rate),2)`, `VatAmount = netPayable - VatableSales`, `VatExemptSales = vatExemptAmount`, `ZeroRatedSales = 0`.

- [ ] **Step 1: Failing unit tests:** `Compute(112m,0)` → vatable 100.00, vat 12.00; `Compute(0m,0)` all zero; senior/PWD sale (gross 224, vatExempt 24, net after 20% = 160) asserts `VatableSales + VatAmount == netPayable` and rounding never off by more than 0.01.
- [ ] **Step 2: Run, expect FAIL.**
- [ ] **Step 3: Implement the calculator** (pure, no DI).
- [ ] **Step 4: Failing integration test `Checkout_StampsVat`** completes a ₱112 cash sale and asserts stored `VatableSales=100`, `VatAmount=12`, `VatRateApplied=0.12`, `VatDerived=false`; second test: migration backfill sets `VatDerived=true` for a pre-seeded completed sale with null stamps.
- [ ] **Step 5: Stamp in the completion step; write the backfill as SQL in the migration** (`WHERE "Status" IN (2,4) AND "VatRateApplied" IS NULL`). Run tests incl. existing `BirReading*` suites unchanged. Expect PASS.
- [ ] **Step 6: Commit.**

### Task 4: Cost snapshot + cost-on-receiving

**Files:**
- Modify: `TransactionService.cs` (`StageLineAsync` L220-229, L289-298), `Catalog/ItemService.cs` + `ItemDto.cs` (`CostPrice` in create/update/response), `Inventory/IncomingReceivingService.cs` (~L117), `TenantSettingsService.cs`
- Test: `Purch.IntegrationTests/CostSnapshotTests.cs`

**Interfaces:**
- Consumes: Task 1 columns. Produces: `ItemDto.CostPrice decimal?`, tenant setting `UpdateCostOnReceiving` on `PUT /tenant/settings/inventory-tracking` payload (extend, don't add route).

- [ ] **Step 1: Failing tests:** adding an item with `CostPrice=40` snapshots `UnitCost=40` on the line; later changing item cost to 55 leaves the existing completed line at 40; item without cost → `UnitCost == null`; recipe item with two ingredients (cost 10×2, 5×1) → 25; receiving with `UpdateCostOnReceiving=true` sets `Item.CostPrice` to the receipt line `UnitPrice`, and with it false leaves it; negative cost → 400.
- [ ] **Step 2: Run, expect FAIL.**
- [ ] **Step 3: Implement** snapshot at stage time (kept on merge of identical plain lines) and validation `CostPrice >= 0`.
- [ ] **Step 4: Run, expect PASS.** Commit.

### Task 5: Park / resume / discard / expire

**Files:**
- Create: `Purch.Application/Pos/CartParkingService.cs`
- Modify: `PosEndpoints.cs`, `ITransactionRepository.cs` + `EfTransactionRepository.cs` (`ListParkedByBranchAsync`), `TransactionService.cs` (`RequireOwnLineAsync` L1850-1856, `CheckoutCoreAsync` L642-666 unaffected but covered by test), `RetentionSweeper.cs`, `ClaimKioskOrderAsync`
- Test: `Purch.IntegrationTests/ParkedCartTests.cs`

**Interfaces:**
- Produces (all `posOperator`, `RequirePermission(Permissions.CartPark)`):
  - `POST /transactions/cart/park` body `{ label?: string }` → `204`; 400 if cart empty or kiosk-claimed.
  - `GET /transactions/parked` → `ParkedCartSummaryDto[] { Id, Label, ParkedAt, ParkedByName, LineCount, TotalAmount }` (branch scoped, newest first).
  - `POST /transactions/{id}/resume` → `TransactionDto` (reprices; adds `ChangedLines: ChangedLineDto[]` = `{ LineId, Reason: "PriceChanged"|"Unavailable" }`; unavailable lines removed).
  - `DELETE /transactions/{id}/parked` (void without payment, audited, `CartExpired` reason `Discarded`).
  - `ICartParkingService.ExpireStaleAsync(DateTimeOffset now, CancellationToken)` used by `RetentionSweeper`.

- [ ] **Step 1: Failing tests (names):** `Park_MovesOpenCartToParkedAndAllowsNewCart`, `Park_EmptyCart_Returns400`, `Resume_SwapsWithNonEmptyOpenCart`, `Resume_DiscardsEmptyOpenCart`, `Resume_OnOtherBranch_Returns403`, `Resume_Concurrent_ExactlyOneSucceeds` (two parallel requests, one 200 one 409), `Resume_RepricesAndReportsChangedLines`, `Checkout_DoesNotVoidParkedCarts`, `Sweeper_VoidsParkedPastTtlWithAudit`, `Park_KioskClaimedOrder_Returns400`.
- [ ] **Step 2: Run, expect FAIL.**
- [ ] **Step 3: Implement the service** inside `unitOfWork.ExecuteInTransactionAsync`; resume sets `DeviceId` to current device and `Status=Open`; rely on the row concurrency token for the 409.
- [ ] **Step 4: Wire endpoints + sweeper** using `Tenant.ParkedCartTtlHours`; write `CartParked/CartResumed/CartExpired` audit rows.
- [ ] **Step 5: Run `ParkedCartTests` + full `PosEndpointsTests` + `RetentionSweeperTests`. Expect PASS.** Commit.

### Task 6: Manual discount + price override

**Files:**
- Create: `Purch.Application/Pos/ManualDiscountService.cs`, `ManualDiscountDto.cs`
- Modify: `TransactionService.RecalculateTotalAsync` (L1901-1957), `PosEndpoints.cs`, `ApplySeniorPwdDiscountAsync` (L1515)
- Test: `Purch.UnitTests/ManualDiscountPricingTests.cs`, `Purch.IntegrationTests/ManualDiscountTests.cs`

**Interfaces:**
- Produces (`posSupervisor`, `RequirePermission(Permissions.DiscountApply / PriceOverride)`, `ShiftApproval` rate limit):
  - `PUT /transactions/cart/lines/{lineId}/discount` `{ kind: "Percent"|"Fixed", value: decimal, reason: string }`; `value: 0` clears.
  - `PUT /transactions/cart/discount` same body for cart level.
  - `PUT /transactions/cart/lines/{lineId}/price` `{ newUnitPrice: decimal, reason: string }`.
- Recalc rule: if any manual discount present → item promos and promo code suppressed for affected lines/cart, `DiscountAmount += ManualDiscountAmount`; Senior/PWD applied → manual discounts cleared with an audit row.

- [ ] **Step 1: Failing unit tests:** 10% on ₱200 line → ₱20 off, VAT on net; fixed ₱500 on ₱200 line → clamped to ₱200 (never negative); cart 10% + line fixed both apply once, cart discount computed after line discounts; promo code present + manual cart discount → promo amount 0.
- [ ] **Step 2: Failing integration tests:** Cashier → 403 on each route; Manager 200 and an `AuditLog` row with reason; empty reason → 400; Senior/PWD after manual discount clears it; manual discount after Senior/PWD → 409.
- [ ] **Step 3: Run, expect FAIL.**
- [ ] **Step 4: Implement**; price override stores `PriceOverrideFrom` = prior unit price and recomputes `LineTotal`.
- [ ] **Step 5: Run unit + integration + `AccessMatrixTests`. Expect PASS.** Commit.

### Task 7: Open item + line note

**Files:**
- Modify: `ManualDiscountService.cs` (add `AddOpenItemAsync`), `TransactionService.cs` stock decrement/refund paths (skip `IsOpenItem`), `PosEndpoints.cs`, receipt DTO mapping to use `OpenItemName`
- Test: `Purch.IntegrationTests/OpenItemTests.cs`

**Interfaces:**
- Produces: `POST /transactions/cart/lines/open` `{ name: string(1..80), unitPrice: decimal(>0), quantity: decimal(>0), note?: string }` → `TransactionDto`; `PUT /transactions/cart/lines/{lineId}/note` `{ note?: string(<=200) }`. `OpenItemEnabled=false` → 403.

- [ ] **Step 1: Failing tests:** open item adds line with null `ItemId`, `UnitCost=null`; checkout completes without touching stock; refund restocks nothing and does not throw; line shows `OpenItemName` on `TransactionDto`; disabled tenant setting → 403; empty name → 400.
- [ ] **Step 2: Run, expect FAIL → implement → run, expect PASS.** Audit `OpenItemAdded`. Commit.

### Task 8: Split tender backend

**Files:**
- Create: `Purch.Application/Pos/SplitTenderService.cs`
- Modify: `PaymentDto.cs` (`RecordPaymentRequest` + `Parts`), `CheckoutRequest.cs`, `TransactionService.cs` (`RecordPaymentCoreAsync` L1023-1117, `RefundTransactionAsync` L923-991, `ReverseCreditChargeAsync` L1164), `ReportingService`/`BirReadingService` cash totals
- Test: `Purch.IntegrationTests/SplitTenderTests.cs`

**Interfaces:**
- Produces: `record PaymentPartRequest(PaymentMethod Method, decimal Amount, decimal? AmountTendered, Guid? CustomerCreditLedgerId, string? Reference)`; `RecordPaymentRequest` gains `IReadOnlyList<PaymentPartRequest>? Parts = null` (null → legacy single payment, behaviour unchanged); `SplitTenderService.Build(decimal total, IReadOnlyList<PaymentPartRequest> parts) -> SplitTenderResult { IReadOnlyList<Payment> Payments, decimal ChangeGiven }` throwing `ValidationException` per the rules in Global Constraints. `TransactionDto.Payments: PaymentPartDto[]`.

- [ ] **Step 1: Failing unit tests on `Build`:** cash 100 + GCash 50 on total 150 ok; parts sum 149.99 → rejected; cash tendered 200 for 100 part gives change 100; GCash overpay → rejected; two Utang parts → rejected; zero/negative amount → rejected; `Split`/`QrPh` methods rejected as before.
- [ ] **Step 2: Failing integration tests:** split checkout writes N rows with `Sequence` 0..n-1 and one receipt number; retry with same `ClientSaleId` returns the same sale without duplicate payments; Utang part respects credit limit and charges only its amount; refund reverses Utang part once; Z-reading cash excludes non-cash parts; single-tender X/Z JSON identical to a stored golden file.
- [ ] **Step 3: Run, expect FAIL.**
- [ ] **Step 4: Implement** and route `RecordPaymentCoreAsync`: when `Parts` present use the service, else keep the legacy path.
- [ ] **Step 5: Run `SplitTenderTests`, `PosEndpointsTests`, `BirReading*`. Expect PASS.** Commit.

### Task 9: Web data layer

**Files:**
- Modify: `web/src/features/pos/api.ts`, `queries.ts`, `types.ts`
- Test: `web/src/features/pos/queries.test.ts` (new)

**Interfaces:**
- Produces in `posApi`: `parkCart(label?)`, `listParked()`, `resumeCart(id)`, `discardParked(id)`, `setLineDiscount(lineId, body)`, `setCartDiscount(body)`, `overrideLinePrice(lineId, body)`, `addOpenItem(body)`, `setLineNote(lineId, note)`; `usePay` accepts `parts?: PaymentPart[]`. Hooks `useParkedCarts()` (key `['pos','parked']`), `useParkCart()`, `useResumeCart()`, with the same `qc.setQueryData(['pos','cart'])` pattern as `useCartMutation`.

- [ ] **Step 1: Failing tests** mocking `apiClient`: resume writes returned cart into `['pos','cart']` and invalidates `['pos','parked']`; `usePay` posts `parts` when given and legacy shape when not.
- [ ] **Step 2: Run** `npm test -- queries.test` in `web/`. Expect FAIL → implement → PASS. Commit.

### Task 10: Web park/resume UI

**Files:**
- Create: `web/src/features/pos/components/ParkedCartsDrawer.tsx`, `ParkCartDialog.tsx`
- Modify: `CartPanel.tsx`, `SellPage.tsx`, `CashierTopBar.tsx` (badge count)
- Test: `ParkedCartsDrawer.test.tsx`

- [ ] **Step 1: Run the three design skills** (see Global Constraints) and record the chosen layout (drawer from the right, ≥48px rows, parked count badge) as a short note at the top of `ParkedCartsDrawer.tsx`.
- [ ] **Step 2: Failing tests:** Park button disabled on empty cart; parking prompts optional label then clears cart; drawer lists parked carts with label/time/total; Resume shows a toast listing `ChangedLines`; Discard asks `ConfirmModal`; cashier sees park/resume, offline state disables with the existing "needs a connection" copy.
- [ ] **Step 3: Implement; run `npm test`, `npm run build`, oxlint. Expect PASS.** Commit.

### Task 11: Web discounts, override, open item, note

**Files:**
- Create: `components/DiscountDialog.tsx`, `OpenItemDialog.tsx`, `LineActionsMenu.tsx`
- Modify: `CartPanel.tsx`, `useApproverGatedAction.ts` use (reuse, no change)
- Test: `DiscountDialog.test.tsx`, `OpenItemDialog.test.tsx`

- [ ] **Step 1: Failing tests:** discount/override actions hidden for Cashier role and shown for Manager (`signInAs`); reason is required (zod schema in `rules.ts`, `rules.test.ts` covers percent 0-100, fixed ≤ line total); open-item dialog validates name/price/qty and adds the line; note saved and shown on the line.
- [ ] **Step 2: Implement per the design-skill pass; run web tests/build/lint. Expect PASS.** Commit.

### Task 12: Web split payment

**Files:**
- Create: `components/SplitTenderPanel.tsx`
- Modify: `PaymentPage.tsx`, `rules.ts` (`remaining`, `validateParts` mirroring server rules)
- Test: `SplitTenderPanel.test.tsx`, extend `PaymentPage` tests; `web/e2e/split-payment.e2e.ts`

- [ ] **Step 1: Failing tests:** add parts until remaining is 0 enables Complete Sale; cash overpay shows change, GCash overpay blocked; second Utang part blocked; Utang part prompts for customer ledger via existing credit picker; single-method flow unchanged (regression).
- [ ] **Step 2: Implement reusing `CashKeypad`; run vitest, build, lint, and the Playwright spec against a local server (`mcp preview_start`).** Expect PASS. Commit.

### Task 13: Web item cost + settings

**Files:**
- Modify: `web/src/features/catalog/pages/ItemDialog.tsx`, `schemas.ts`, tenant settings page under `web/src/features/tenant` (parked TTL, open item, cost-on-receiving)
- Test: `schemas.test.ts`, settings page test

- [ ] **Step 1: Failing tests:** cost optional, ≥ 0; margin hint shows `(price - cost)/price` only when cost present; settings save calls the extended endpoints.
- [ ] **Step 2: Implement; run tests/build/lint. Expect PASS.** Commit.

### Task 14: Regression gate and handoff

- [ ] **Step 1:** Run full backend suite `dotnet test` (D:\dotnet-sdk) and `dotnet ef migrations has-pending-model-changes`. Expect all green, no pending changes.
- [ ] **Step 2:** `npm test`, `npm run build`, lint, `npm run e2e` in `web/`. Expect green.
- [ ] **Step 3:** Update `docs/system-analysis/10-feature-inventory.md` with rows POS-15..POS-21 (park, resume, discard, manual discount, price override, open item, split tender) and `PRODUCT.md` "Evidence on Hand" if counts changed.
- [ ] **Step 4:** Record golden pricing cases (manual discount, split tender totals, VAT stamp) in `docs/superpowers/specs/u1-golden-cases.json` for U6 Flutter mirroring. Commit; open PR.

## Notes
- U1.5 (attach customer) is intentionally shipped as the `Transaction.CustomerId` column only (Task 1); its endpoint lands in U2 with the `Customer` entity, resolving the coupling flagged in the spec.
- Task order is strict: 1 → 2 → 3/4 (parallel-safe) → 5 → 6 → 7 → 8 → 9 → 10-13 → 14.
