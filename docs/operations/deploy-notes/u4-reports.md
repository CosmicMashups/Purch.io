# Deployment note: U4 reports

Reporting only: new read endpoints, web tabs and one index migration. No new tables, no data change, no
Flutter change.

## Migration

`AddReportingRefundAndAuditIndexes`: two indexes, both created online-safe for a small table but not
`CONCURRENTLY` (EF migrations run in a transaction):

- `IX_Transactions_Refunded` on `Transactions (TenantId, BranchId, RefundedAt) WHERE "RefundedAt" IS NOT NULL`
- `IX_AuditLogs_Action_CreatedAt` on `AuditLogs (TenantId, ActionType, CreatedAt)`

Apply it out-of-band before traffic, as for U1 (cold-start auto-apply failure is non-fatal). The new code does not
need the indexes to be correct, only to be fast, so the deploy order is not critical. On a large `AuditLogs`
table, create the index `CONCURRENTLY` by hand first and mark the migration applied.

## Behaviour to know

- New routes: `/reports/sales-mix`, `/tax`, `/gross-profit`, `/discounts`, `/discounts/manual`, `/voids`,
  `/refunds`, `/dashboard-insights` (Admin and Manager) and `/voids.csv`, `/refunds.csv`,
  `/discounts/manual.csv` (Admin).
- Aggregate ranges over 366 days return 400.
- Tax totals use stamped VAT and can differ from X/Z by centavos (documented in the spec).
