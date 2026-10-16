# Deployment note: U3 inventory depth

Web and backend only. No Flutter change. Stock figures are tenant-wide (no per-branch valuation, count or import).

## Migrations (apply in order, out-of-band, before traffic)

1. `20261015162436_AddStockTakes`: `StockTakes`, `StockTakeLines`.
2. `20261015163631_AddStockTakeLineVersion`: empty `Up`; maps the Postgres `xmin` row version on `StockTakeLines`. Model snapshot only.
3. `20261015171104_AddImportBatches`: `ImportBatches` (unique `(TenantId, IdempotencyKey)`).
4. `20261015174131_ImportBatchRowVersion`: empty `Up`; `xmin` row version on `ImportBatches`. Model snapshot only.

Run them by hand first, as for U1 and U4: cold-start auto-apply failure is non-fatal, so the app can start against an
un-migrated database and then fail on the new routes. Migrations 2 and 4 change nothing in the database.

## Row level security

`ENABLE ROW LEVEL SECURITY` and the tenant policy are in the migrations for `StockTakes`, `StockTakeLines` and
`ImportBatches` (`RowLevelSecurityCoverageTests` covers them). Nothing to do by hand.

## New routes

- Reports (Admin, Manager, Warehouse): `GET /reports/inventory/valuation`, `GET /reports/inventory/cogs`.
- Export (Admin, Manager, Warehouse): `GET /inventory/export/items.csv`, `/ingredients.csv`, `/stock.csv`.
- Stock-take (Admin, Manager, Warehouse): `POST/GET /stock-takes`, `GET /stock-takes/{id}`,
  `PUT /stock-takes/{id}/lines/{lineId}`, `POST /stock-takes/{id}/start`, `/cancel`.
  `/review` is Manager/Admin; `/apply` is Manager/Admin with an approver PIN.
- Import (Admin, Manager, Warehouse): `POST /inventory/imports`, `GET /inventory/imports/{id}`,
  `PUT /inventory/imports/{id}/resolutions`. `POST /inventory/imports/{id}/commit` is Manager/Admin with an approver PIN.
- Web: `/inventory/stock-takes`, `/inventory/stock-takes/:id`, `/inventory/import`, valuation and COGS tabs.
- Cashier gets 403 on all of these and never receives cost fields.

## Behaviour to know

- Stock-take apply posts `counted - expected_at_count` as a movement delta, so sales after the count are kept.
  Apply may leave stock negative. Uncounted and skipped lines are untouched.
- Import caps: 5,000 rows, 5 MB. All-or-nothing commit; new categories and records are created only after the user
  confirms them. A catalog import never changes stock of matched rows; a stock import posts movements and needs a branch.
- Valuation excludes unknown cost (never 0) and reports how many were excluded. COGS ranges over 366 days return 400.

## Rollback

Code rollback is safe: old code ignores the new tables. To drop the data, roll the database back to
`20261015140611_AddIncomingReceivingControlNumber` (migrations 4 to 1 in reverse). That deletes all stock takes and
import batches; stock movements already posted by an apply or import stay in `InventoryMovements` and are not reversed.
