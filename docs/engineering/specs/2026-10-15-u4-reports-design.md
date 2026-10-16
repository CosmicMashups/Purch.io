# U4 Reports: design

Status: implemented on `feat/reporting-sales-tax-margin-discounts`. Owner decisions are recorded inline.

## Scope

Sales by payment type, category and hour; range tax report; gross profit and margin; discount, void and refund
reports; three executive dashboard cards. Each report has CSV and PDF export. X/Z readings, BIR receipts, offline
sync, Senior/PWD, tingi, utang and kiosk/KDS are untouched. No tier gating.

Audit before building: X/Z, the daily sales dashboard, department sales, staff performance, the single-day
approvals review and the Admin transactions CSV already existed. Everything in scope above was missing or only
available as device-scoped X/Z totals.

## Rules every report follows

- **Sales are gross.** Completed and Refunded sales, dated by `CompletedAt ?? CreatedAt`. A refund never
  changes a past day; refunds are reported on their own by `RefundedAt` (matches X/Z, which includes a refunded
  sale and subtracts the refund in the period it happened).
- **Business day is UTC+8.** Queries bucket by UTC hour in SQL; services shift by `ReportTimeZone.Offset`
  (whole hours, so exact).
- **Range:** at most 366 days (400 above), `to` exclusive. Listings page server-side (default 50, max 200);
  CSV listings refuse over 20,000 rows rather than truncate.
- **Scope:** branch scope comes from `ReportScopeResolver`. Admin and Manager only; Cashier gets 403.
  Per-transaction listing CSVs (void, refund, manual discount) are Admin only because they name staff.
- **One request per tab.** Aggregates return everything a tab needs in one response; sub-views switch on the
  client. Web caches for 60s, no refetch on focus, previous data kept while a range loads.

## Reports

| Report | Endpoint | Notes |
|---|---|---|
| Sales mix | `GET /reports/sales-mix` | Payment type, category, 24 hour buckets, weekday-by-hour heatmap |
| Tax | `GET /reports/tax` | Per day and total from the stamped VAT columns |
| Gross profit | `GET /reports/gross-profit` | Totals, per day, by category, top and bottom 10 items |
| Discounts | `GET /reports/discounts`, `/discounts/manual` | By type, cashier and approver; manual rows with reason |
| Voids and refunds | `GET /reports/voids`, `/refunds` | Lists plus exchanges |
| Dashboard | `GET /reports/dashboard-insights` | Margin trend, leakage, category share |
| Listing CSVs (Admin) | `/reports/voids.csv`, `/refunds.csv`, `/discounts/manual.csv` | |

### Payment type

Sums `Payment` rows with `Status = Confirmed`, grouped by method. `Payment.Amount` is the applied amount
(change is `AmountTendered - Amount`), so cash is already net of change. A split sale contributes one row per
part. `UtangCredit` is credit extended and is flagged, not counted as cash. Exchange price differences are shown
as a separate line so the drawer reconciles.

### Category

Joins the item's **current** `CategoryId` (lines carry no category snapshot), like department sales. Null or
deleted category is "Uncategorised"; open items are their own "Open items" bucket so totals reconcile. History
follows current categorisation; this is stated on screen.

### Hour

24 buckets with sales, count, average basket and average per operating day (distinct business days with at
least one sale in the range), plus a weekday-by-hour heatmap.

### Tax (decision: stamped columns are authoritative)

Sums `VatableSales`, `VatAmount`, `VatExemptSales`, `ZeroRatedSales`. Stamps are rounded per sale, so a range
total can differ from the sum of X/Z readings by centavos; X/Z still recomputes on the fly and stays
byte-identical. `VatDerived` rows (backfilled history, legacy 12% rule) are counted and surfaced. Senior/PWD
exempt is reported as stamped (`Total + DiscountAmount`, which includes promo discounts on that sale, matching
the legacy X/Z) with an on-screen note; revisit before BIR accreditation relies on it.

### Gross profit and margin

- Revenue is net of discounts and excludes VAT: the stamped vatable + exempt + zero-rated sales.
- Cost is `Quantity * UnitCost` over lines whose sale-time `UnitCost` is known. Null is never zero. Open items
  have no cost.
- Revenue is attributed to lines in proportion to line totals, so sale-level discounts spread evenly across the
  lines of a sale (approximation, per day and over the range). Unknown-cost value is reported as "left out" and
  coverage % is shown.
- Margin = `(known revenue - cost) / known revenue`.

### Discounts

Same split as X/Z: promo code is `PromoDiscountAmount`; Senior/PWD is `DiscountAmount - PromoDiscountAmount` on
Senior/PWD sales; manual is the remainder of `DiscountAmount`; item promos are `ItemPromoDiscountAmount`.
Approvers come from `AuditLog.ApprovedByUserId` (DiscountOverride, PriceOverride). Manual discounts are listed
from the sales themselves (so a discount on a cart that was later abandoned never appears), with the reason from
the sale and the latest approving audit entry.

### Voids (decision: match X/Z on parked carts)

Voided carts that had value (`TotalAmount > 0`), with `ParkedAt` null, dated by the void audit entry. Parked
carts the retention sweep expired and zero-value discards are excluded and reported as two counts, so the total
can differ from the X/Z voided count (which includes empty discards).

### Refunds and exchanges

Refunds by `RefundedAt` with reason and approver from the refund audit entry. Exchanges (`Adjustment`) are
listed beside them with returned, replacement and price difference.

## Dashboard cards (executive)

Only three, delivered by one lazy call fetched when the section nears the viewport: gross margin trend (with
cost coverage), revenue leakage (discounts + voids + refunds as % of sales before discounts) and category share
with margin. Hour, payment mix and VAT stay in the Reports tabs. The existing `sales-dashboard` payload is
unchanged. New series use the selected range in UTC+8 business days, like the Reports tabs.

## Export

- CSV: built client-side from the cached response (no extra request); formula-injection guard in `csv.ts`
  mirrors `ReportCsv.Field` on the server.
- PDF: print layout plus the browser's Save as PDF (no server PDF engine, no dependency). Each print carries
  period, generated time and the report's caveats.

## Schema

No new tables. Indexes: `IX_Transactions_Refunded` (partial, `TenantId, BranchId, RefundedAt`) and
`IX_AuditLogs_Action_CreatedAt` (`TenantId, ActionType, CreatedAt`). Migration `AddReportingRefundAndAuditIndexes`.

## Known limits

- Category and open items follow current catalog state; lines have no category snapshot.
- Profit attribution is proportional per day/range, not per line.
- Voids are dated by the audit entry, which exists only for carts that had content.
- Aggregates are not cached server-side (per-instance on serverless); the web cache covers repeat views.
