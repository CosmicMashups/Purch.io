# P1 competitor-gap roadmap: status and handoff (as of 2026-10-15)

Read this first in a new session. It supersedes the unit order in the original plan header.

## Where things stand
- **U4 (Reports) has shipped** (on `origin/main`). Spec: `docs/engineering/specs/2026-10-15-u4-reports-design.md`. Deploy: `docs/operations/deploy-notes/u4-reports.md`.
- **U3 (Inventory depth) has shipped** (merged to `main` at `bc323cb`). Spec: `docs/engineering/specs/2026-10-15-u3-inventory-depth-design.md`. Plan: `docs/engineering/plans/2026-10-15-u3-inventory-depth.md`. Deploy: `docs/operations/deploy-notes/u3-inventory-depth.md`. Owner decisions: variance is `counted - expected_at_count` applied as a delta; import creates items/categories after user review; stock is tenant-wide (no per-branch); web only, Flutter unchanged.
- **U1 (POS foundations) is done and merged to `main`** at `d70be7c` (local, not pushed). Spec: `docs/engineering/specs/2026-10-14-u1-pos-foundations-design.md`. Plan: `docs/engineering/plans/2026-10-14-u1-pos-foundations.md`. Deploy steps: `docs/operations/deploy-notes/u1-pos-foundations.md`. Golden pricing cases for Flutter: `docs/engineering/specs/u1-golden-cases.json`. Feature rows: `docs/product/feature-inventory.md`.
- Last verified: backend unit 136 + integration 538 (clean, no cleanup failures); web 1053 tests, build and lint clean; Playwright has 8 failures that also fail on the pre-U1 baseline `12f3b3f` (admin x2, business-dialogs, dashboards, offline x2, pos device-gate, roles kiosk). They are not caused by U1 and still need their own look.
- Flutter client is **unchanged**. No pricing/tier gating exists anywhere; a later session handles tiers.

## Order from here (owner decision 2026-10-15)
1. **U4 Reports**, then 2. **U3 Inventory depth**, then U2 Customers + loyalty, U5 Staff (time clock, custom roles), U6 Flutter parity. P2 and P3 stay as listed in the original request.
Each unit gets its own brainstorm, spec, plan and tests (own PR). U4 and U3 are independent of each other; both only need data U1 already ships.

## U4 Reports: what to know
Scope: sales by payment type / category / hour, tax report, gross profit and margin, discount / void / refund reports (extend `backend/src/Purch.Api/Endpoints/ReportingEndpoints.cs`, CSV export per report, indexes).
- **Tax report reads the stamped columns** on `Transaction` (`VatableSales`, `VatAmount`, `VatExemptSales`, `ZeroRatedSales`, `VatRateApplied`, `VatDerived`), not an on-the-fly recompute. `BirReadingService` still computes on the fly on purpose, to keep X/Z byte-identical (Ruling 5). Stamps are per-sale rounded, so a range total can differ from X/Z by centavos: decide and document which is authoritative.
- Backfilled history has `VatDerived = true` (legacy 12% rule). Surface that flag.
- **Senior/PWD exempt** is stamped as `Total + DiscountAmount`, which includes promo discounts on a Senior sale (matches legacy). Revisit before a BIR tax report relies on it.
- **Margin:** `TransactionLine.UnitCost` is the sale-time snapshot; `null` means unknown. Exclude unknown-cost lines from margin and report how many were excluded. Never treat null as 0. Open items have no cost. Cost is redacted for the Cashier role: gross profit and margin reports are Manager/Admin.
- **Payment type:** a sale can have several `Payment` rows (`Sequence`, `Reference`). Sum rows, with cash net of change; never read one method per sale. Utang parts are credit, not cash.
- **Voids:** `Status = Voided` carts that were parked and expired by the sweeper (`ParkedAt` still set) are intentionally excluded from X/Z voided totals; approved discards and staff voids clear `ParkedAt` and count. Decide the same rule for the void report. Orders history lists expired parked carts as Voided (accepted, Ruling 8).
- **Discounts:** manual discounts store kind (`Percent`/`Fixed`), value, reason, computed amount on lines and cart; X/Z folds them into the promo-discount line today (no separate BIR line). The discount report should split promo code / item promo / manual / Senior-PWD, with approver from `AuditLog.ApprovedByUserId`.
- Open items are missing from top-items (in totals and in the null-department bucket): decide whether the report should list them.

## U3 Inventory depth: what to know
Scope: valuation (on-hand x cost, by category/branch), COGS from line snapshots, CSV export/import of items and stock, stock-take sessions with variance.
- Item `CostPrice` and ingredient `UnitCost` exist; update-keeps-on-omit, `ClearCostPrice` / `ClearUnitCost` to unset. Optional last-cost update on receiving (`Tenant.UpdateCostOnReceiving`, default off, only lines with `UnitPrice > 0`).
- Agreed design (from the original grill): two-step import (upload, dry-run preview, commit), match SKU then barcode, all-or-nothing per file, caps ~5,000 rows / 5MB, stock import posts `InventoryMovement` (not overwrite), idempotency key, export columns round-trip with import. Stock-take: draft, counting, review, applied; per-line expected snapshot, variance = counted - (expected + movements since), blind-count option, Manager approval, audit, no sales lock.
- New tables need `ENABLE ROW LEVEL SECURITY` in their migration (`RowLevelSecurityCoverageTests`).

## Standing decisions and project rules
- **Discounts / price overrides / any supervisor-style action:** performed in a Cashier session with a Manager/Admin approver PIN every time, never gated by session role alone (memory: `project_discount_approval_pin`). Senior/PWD discount remains Manager/Admin only.
- **Approver PIN lockout:** call `ApproverAuthorizationService.AuthorizeAsync` BEFORE opening `ExecuteInTransactionAsync`; a rollback otherwise discards the failed-attempt counter and the 5-failure device lockout never triggers.
- **Transaction retries:** `EfUnitOfWork` calls `ChangeTracker.Clear()` on an execution-strategy retry, so load entities INSIDE the lambda. `ExecuteInTransactionAsync` is not re-entrant.
- Cart cap: `TransactionService.MaxCartAmount` (9,999,999,999.99, `numeric(12,2)`), enforced centrally in `RecalculateTotalAsync`.
- Existing-cart tests: `Parked` is status value 5 (appended); the one-open-cart index filters on `Status = 0`.
- Web: hand-rolled components in `web/src/components`, Phosphor icons, tokens in `docs/design/tokens.json` (variance 6, motion 4, density 3), >= 48px targets, role gating hides (not disables). Run `/impeccable`, `/design-taste-frontend` and `/ui-ux-pro-max:ui-ux-pro-max` before UI work.

## Environment notes
- .NET SDK is at `D:\dotnet-sdk` (no system SDK); `dotnet-ef` design-time needs `PURCH_DEPLOYMENT_MODE=Local`, `LOCAL_DB_CONNECTION_STRING`, `LOCAL_STORAGE_PATH` (dummy values are fine). Integration tests need Docker Desktop (`D:\Programs\DockerDesktop`) running and take ~18-22 minutes; run the full suite once, last, with no edits afterwards.
- E2E: `web/e2e` against the local e2e stack with the e2e seed credentials, localhost only.
- Production deploy: backend is Vercel-only against Supabase; migrations auto-apply at cold start and failure is non-fatal, so run migrations out-of-band first (see the deploy note).

## Parked items from U1 (none blocks U4 or U3)
Resolved 2026-10-16: payment idempotency key (`PaymentKey` on `POST /transactions/cart/payments`, stored in `ClientSaleId`); typed `resource`/`code` on problem details (web matches those, not message text); Utang credit-limit override UI (cashier supplies an Admin/Manager approver PIN, audited with `ApprovedByUserId`); the missing tests except the approver-PIN retry test; `RolesFor` unknown-key guard; `/items` ETag uses `CanSeeCosts`; audit entry on parked-cart TTL change; stale comments; parked-list and ingredient-cost N+1s; web polish (tokens, 48px switches, `aria-describedby`).
- **Approver branch scope (decided, enforced):** an Admin/Manager PIN only counts at the signed-in branch, or when that account has no branch (tenant-wide).
- Still open: the retry test with an approver PIN (needs a fault-injecting unit of work); combo-cost N+1 (not found in the code, may already be gone); `PurchaseRequestService` still looks up unit costs one by one.
- Flutter (U6): must never receive a Parked transaction (status index 5 exceeds its enum); open items arrive with an all-zero `itemId`; local `PricingEngine` must mirror manual discounts, split tender, VAT stamping and the cart cap (use the golden-case file); offline checkout cannot carry open items or split tender yet; Flutter must send `paymentKey`.

## U3 Inventory depth: follow-ups and rulings to confirm (2026-10-16)
Open follow-ups:
- Per-branch stock: valuation, count and import stay tenant-wide (ADR 0008). Treat as final unless the owner says otherwise; tiering by location depends on it.
- Flutter parity (U6).
- Stock import with no branch is handled (`RequiresBranch`, web picker). Stock-take apply rewrites `UpdatedAt` on every line on purpose: it bumps the `xmin` token so a stale count/skip fails with 409. The in-transaction stale check for import commit is unit-covered only (an integration test needs a fault-injecting unit of work).
- COGS is now aggregated in SQL (per business day, item, category, open-item flag).
- Web minors fixed: Review recount list is collapsed, the item picker skips items without their own stock, `day()` uses `businessToday`.

Rulings to confirm (product calls made during the build):
- `/review` is Manager/Admin only (blind-count integrity): Warehouse counters need a manager to move a take to Review.
- Apply may drive stock negative (delta semantic); negative lines are listed in the audit.
- No Draft scope edit (cancel and recreate); no back-to-Counting transition (recount in Review instead).
- Import near-match thresholds: distance 1 needs both keys >= 4 chars, distance 2 both >= 6, keys differing only in digits are not near. Cost if wrong: a typo on a short name creates a near-duplicate instead of asking.
- Import: negative price/cost/reorder/counted_qty are row errors; `opening_stock` on matched rows is a row error; rows with no SKU/barcode match by name key; an expired batch returns 400 (not 410).
- Import commit takes an optional BranchId (required for stock imports); web admins must pick a branch.
- Line-level `xmin` concurrency token on `StockTakeLine` (own migration) instead of bumping the take row per count.
