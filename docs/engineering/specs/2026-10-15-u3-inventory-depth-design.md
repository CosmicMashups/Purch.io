# U3 Inventory depth: design

Date: 2026-10-15. Status: draft for owner review. Branch: `feat/u3-inventory-depth` (from `origin/main` at `f713a10`).

## Goal
Give managers a trustworthy picture of what stock is worth and what it cost to sell, and safe bulk tools to correct and load stock: inventory valuation, inventory-side COGS, CSV export/import of items, ingredients and stock, and stock-take sessions with variance.

## Verified gaps on origin/main
| Capability | State |
|---|---|
| Valuation (on-hand x cost) | Missing |
| COGS | Partial: `TransactionLine.UnitCost` snapshot and `GET /reports/gross-profit` exist (U4). No inventory-side COGS view. |
| CSV export/import of items/ingredients/stock | Missing. Only report CSVs exist (low-stock, transactions). |
| Stock-take sessions | Missing. Only the single-item overwrite `POST /inventory-items/{id}/physical-count`. |

Out of scope (exists): purchase orders, purchase requests, supplier unit cost, VAT/discount on orders, `Item.CostPrice`, `InventoryItem.UnitCost` (+Clear flags), `Tenant.UpdateCostOnReceiving`, Cashier cost redaction.

## Constraints and decisions
- **Tenant-wide stock only.** `Item.StockOnHand` and `InventoryItem.QuantityOnHand` are single values; branch exists only on `InventoryMovement`. No per-branch valuation, count or import in U3. (Per-branch stock is a separate future unit.)
- Web only; Flutter unchanged.
- Roles: Warehouse/Manager/Admin create, count and export; **Manager/Admin** apply a stock-take and commit an import, each with an approver PIN. Valuation/COGS: Manager/Admin/Warehouse. Cashier gets 403 and never receives cost fields (`ICurrentActorProvider.CanSeeCosts`).
- Approver PIN: `ApproverAuthorizationService.AuthorizeAsync` BEFORE `ExecuteInTransactionAsync`; load entities INSIDE the lambda; approver id on `AuditLog.ApprovedByUserId`.
- New tables: `ENABLE ROW LEVEL SECURITY` in the migration.
- Unknown cost is never 0: excluded from totals, counted and listed.

## Slice 1: Valuation and COGS (read-only, no tables)
- `GET /reports/inventory/valuation?categoryId=&type=item|ingredient|all&asOf=now` returns rows (id, name, sku, category, on-hand, unit cost, value) with group subtotals by category and totals: `totalValue`, `valuedCount`, `unknownCostCount`, `negativeStockCount`. Items value at `CostPrice`; ingredients at `UnitCost`. Negative on-hand is valued 0 and flagged. Inactive included; soft-deleted excluded.
- `GET /reports/inventory/cogs?from=&to=&groupBy=item|category|day` sums `qty x TransactionLine.UnitCost` over completed sale lines, net of refunded/voided lines, with `knownCostRevenue`, `cogs`, `coveragePercent`, `excludedLineCount`. Same range limits as U4 (366 days). Recipe consumption is not added (snapshot sits on the sold item); a note says so.
- Web: Reports tabs "Valuation" and "COGS" (role-gated), hub tile on inventory home, CSV via `reports/csv.ts`, caveats on screen and in the print layout.

## Slice 2: Stock-take
### Model
`StockTake` (tenant-scoped): `Id, Name, Status (Draft|Counting|Review|Applied|Cancelled), Blind, ScopeKind (All|Categories|Picked), CreatedBy, StartedAt, AppliedAt, ApprovedByUserId, Note`.
`StockTakeLine`: `Id, StockTakeId, ItemId? | InventoryItemId? (exactly one), CategoryId snapshot, ExpectedQty? (null until counted), CountedQty?, State (Uncounted|Counted|Skipped), CountedBy, CountedAt, VarianceQty?, UnitCostSnapshot?, MovementId?`. Unique `(StockTakeId, ItemId|InventoryItemId)`.

### Lifecycle
- **Draft**: pick scope; lines are materialized from current items/ingredients (name and cost snapshot only). Editable scope.
- **Counting**: counters enter quantities. On each count entry the server snapshots `ExpectedQty` = current on-hand at that instant. Re-entering a count re-snapshots. Blind mode hides `ExpectedQty` from API responses while Counting.
- **Review**: lines show counted, expected, variance, value impact (Manager/Admin/Warehouse only), and "movements since count" for information. Lines with large post-count activity are flagged for recount. Uncounted lines are listed prominently.
- **Applied**: Manager/Admin approves with approver PIN. Each `Counted` line posts a signed `Adjustment` movement of `variance = counted - expected_at_count`. Stock is never set to `counted`, so sales after the count survive. `Uncounted` and `Skipped` lines post nothing and are recorded in the audit entry. Apply is blocked only if zero lines are counted. Idempotent: re-apply of an Applied take is a no-op returning the first result.
- Overlapping open takes on one item are allowed (creation warns); delta-based apply keeps them safe. No sales lock.

### API (`InventoryEndpoints`, group `inventoryManager`)
`POST /stock-takes`, `GET /stock-takes`, `GET /stock-takes/{id}`, `PUT /stock-takes/{id}/lines/{lineId}` (count/skip/unskip), `POST /stock-takes/{id}/start`, `/review`, `/apply` (body `approverPin`, Manager/Admin approver only), `/cancel`.

### Web
`/inventory/stock-takes` list and `/inventory/stock-takes/:id` detail (record-detail pattern from U4 work). Counting view is a large-target numeric entry list, 48px minimum. Generalize `useApproverGatedAction` so it is not tied to the POS cart cache.

## Slice 3: CSV export and import
### Export
`GET /inventory/export/items.csv`, `/ingredients.csv`, `/stock.csv`. UTF-8 with BOM, formula-injection guard (`csvField`). Columns are exactly the importable columns plus read-only context columns (`id`, `current_stock`, `updated_at`) that import ignores. Cost columns are absent for any role without `CanSeeCosts`.

### Import (two-step, all-or-nothing)
1. **Upload** `POST /inventory/imports` (multipart, kind = `catalog` or `stock`, `Idempotency-Key`). Caps 5 MB / 5,000 rows; BOM tolerated; encoding and header validated. Parse and create an `ImportBatch` (`Status Previewed`, content hash, parsed rows, resolutions, 24h expiry; RLS).
2. **Review** `GET /inventory/imports/{id}`: three groups.
   - *Will create*: new categories and new items/ingredients, listed for explicit confirmation.
   - *Needs decision*: possible duplicates, each shown beside nearest existing matches; user chooses link / create new / rename. Resolutions are saved on the batch via `PUT /inventory/imports/{id}/resolutions` and survive re-validation.
   - *Will update*: matched rows with before/after diff.
   Commit is blocked until every decision is made and the creations are confirmed.
3. **Commit** `POST /inventory/imports/{id}/commit` with `approverPin` (Manager/Admin approver). Re-validates inside the transaction; if any row's outcome differs from the preview the whole batch aborts with nothing applied and returns to Previewed with changed rows highlighted (decisions kept). Committed batches are idempotent by batch id + content hash.

### Matching rules
- Rows match SKU first, then barcode (exact after trim; barcodes compared as text, leading zeros preserved).
- Name/category comparison key: trim, collapse whitespace, case-fold, strip diacritics and punctuation, `&` = `and`. Identical keys auto-link, visibly and undoably. Near matches (edit distance <= 2) always ask. Names that collapse to one key within the file group as one new category.
- Blank cell = leave unchanged; `CLEAR` clears an optional cost; literal `0` is a real zero.
- New rows require `type` (item|ingredient), name, and SKU or barcode; a SKU/barcode colliding with another record or duplicated in the file is a row error. Opening stock for a new row posts `StockIn`.
- Catalog import also accepts an import-only `opening_stock` column (never exported): new rows only, posted as `StockIn`; filled on a matched row it is a row error.
- Stock import: SKU/barcode + counted qty (+ note). Posts `Adjustment` of `counted - current` computed at commit time, tagged with the batch id. Never overwrites.

## Data and migration
New tables: `StockTake`, `StockTakeLine`, `ImportBatch` (+ index `(TenantId, IdempotencyKey)` unique). Optional nullable `InventoryMovement.SourceKind/SourceId` to trace stock-take and import adjustments. Every table: `ENABLE ROW LEVEL SECURITY` (`RowLevelSecurityCoverageTests`).

## Audit
Stock-take apply and import commit write `AuditLog` with approver, counts, created categories/items, uncounted lines, batch/take id.

## Testing
Unit: variance maths (post-count sale does not create phantom variance), normalization/matching, valuation unknown-cost handling, COGS net of refunds. Integration (Docker): RLS coverage, role matrix (Cashier 403 and cost redaction), PIN lockout path, apply idempotency, stale-commit abort, caps. Web vitest for panels/dialogs and resolution flow; e2e for stock-take and import happy paths. Known 8 pre-existing e2e failures excluded.

## Open defaults to confirm
Blind mode hides expected only while Counting; import caps 5,000/5 MB; `asOf` valuation is "now" only (no historical valuation).

## Housekeeping
Update `docs/superpowers/plans/P1-roadmap-status.md` (U4 merged, U3 in progress).
