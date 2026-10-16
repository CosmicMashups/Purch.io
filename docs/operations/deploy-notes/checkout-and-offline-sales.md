# Deployment note — one-call checkout, offline sales, Z-reading fix

Covers the backend, database and client changes made together in this batch.
Read the **Before you deploy** section first: the changes were written without
a .NET SDK available, so **none of the backend code has been compiled or run**.

## What changed

| Area | Change |
| --- | --- |
| Backend | New `POST /transactions/checkout` — a whole sale in one idempotent call (keyed on a client-generated `saleId`). Optional device-issued `receiptNumber`, `offlineSale` and `soldAt`. |
| Backend | New `GET /transactions/receipt-sequence` — the highest receipt number recorded for the calling terminal. |
| Backend | BIR X/Z-readings now cover every completed sale **not yet reported**, instead of "receipt numbers above the last reading". Readings also list late-synced and missing receipt numbers. |
| Backend | Refresh-token rotation is atomic, with a 60-second grace window; deactivated users can no longer refresh. |
| Backend | Database errors are no longer reported as "unable to reach the database" (only genuine connection failures are 503). Connection retries added. |
| Backend | Pending EF migrations are applied at startup in Cloud mode (non-fatal — see below). |
| Database | 2 new migrations (below). |
| Flutter client | Local-first Cashier cart, client-side pricing, device-issued receipt numbers, offline sale queue with limits, session reset on login/logout, cache/refresh changes. Local SQLite schema goes 4 → 6 (migrates itself on first launch). |
| Web client | Refresh no longer signs you out on a network error/5xx; tabs share rotated tokens. |

## Database migrations

New in this batch, in order:

1. `20260921000000_AddTransactionClientSaleId` — adds `Transactions.ClientSaleId` (nullable `uuid`) and a **filtered unique index** `(TenantId, ClientSaleId) WHERE ClientSaleId IS NOT NULL`. Pure add; safe on existing data.
2. `20260921000001_AddTransactionZReadingNumber` — adds `Transactions.ZReadingNumber` (nullable `integer`), then **back-fills** it:

   ```sql
   UPDATE "Transactions" t SET "ZReadingNumber" = 0
   FROM "ReceiptSequences" s
   WHERE t."TenantId" = s."TenantId" AND t."BranchId" = s."BranchId" AND t."DeviceId" = s."DeviceId"
     AND t."Status" = 2                                   -- TransactionStatus.Completed
     AND t."ReceiptNumber" IS NOT NULL
     AND t."ReceiptNumber" <= s."LastZReadingReceiptNumber";
   ```

   `0` means "reported by a Z-reading from before this was tracked". **Do not skip the back-fill**: without it, the first Z-reading after deploy would re-report every historical sale and double the grand accumulated sales.

Both migrations were written by hand (no `dotnet ef`), and their `Designer.cs` files do not exist — the migration class carries the `[DbContext]`/`[Migration]` attributes itself, and `PurchDbContextModelSnapshot.cs` was edited by hand.

### Other migrations that may also be pending

The production Supabase database was probably missing several earlier migrations too (the Vercel deploy never ran them, and that is the likeliest cause of the "Items"/"Dashboard" database errors). Compare against the database:

```sql
SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";
```

Migrations that were added recently and may be missing:

- `20260918055430_AddInventoryItemsAndRecipes`
- `20260918070949_AddAutomaticItemPromos`
- `20260919000000_AddRefreshTokens`
- `20260920000000_AddPasswordResetTokens`
- `20260920000000_AddTransactionKitchenStatus`
- `20260920000001_AddDeviceSessionVersion`
- the two above

Two migrations share the `20260920000000` prefix; that is fine (the full name is the key).

### How they get applied

- **Automatically:** the API now runs pending migrations at startup in Cloud mode. This is **deliberately non-fatal** — if it fails (Supabase's pooler can reject migration DDL), the API still starts and logs `Automatic migration failed; run scripts/migrate-production against the database.` Search the logs for that line after deploy.
- **Manually (recommended for this batch):** run `scripts/migrate-production.ps1` (or `.sh`) against the **direct** database connection, not the pooler, *before* the new API version takes traffic. This is safer than relying on startup for a migration that back-fills data.

## Before you deploy

1. **Build and test the backend.** From `backend/`:
   ```bash
   dotnet build Purch.sln
   dotnet test Purch.sln          # integration tests need Docker (Postgres container)
   ```
   The build treats warnings as errors with the *Recommended* analyzer set, so expect to fix a few things the first time. New tests to look at: `PosEndpointsTests` (checkout, idempotency, receipt numbers, offline), `ReportingEndpointsTests` (late-synced sale), `AuthEndpointsTests` (refresh grace / logout).
2. **Confirm the model snapshot matches.** Since the snapshot was hand-edited:
   ```bash
   dotnet ef migrations has-pending-model-changes --project src/Purch.Infrastructure --startup-project src/Purch.Api
   ```
   It should report no pending changes. If it does, regenerate rather than patch.
3. **Rehearse the migrations on a copy of production** (Supabase branch or a restored backup), and check that the back-fill marked the expected rows:
   ```sql
   -- every completed sale at or below its terminal's last Z-reading number should be marked 0
   SELECT count(*) FROM "Transactions" t
   JOIN "ReceiptSequences" s USING ("TenantId","BranchId","DeviceId")
   WHERE t."Status" = 2 AND t."ReceiptNumber" <= s."LastZReadingReceiptNumber" AND t."ZReadingNumber" IS DISTINCT FROM 0;   -- expect 0
   ```
4. **Take a database backup** (Supabase → Database → Backups) immediately before migrating.

## Deployment order

The order matters — a new client talking to an old server fails at payment.

1. **Migrate the database** (manual script, above).
2. **Deploy the backend.** Old clients keep working against the new backend (the old cart endpoints are unchanged and still issue server-side receipt numbers).
3. **Roll out the Flutter client / web app** to each terminal. **Do not** release the new Flutter client before the backend: it calls `/transactions/checkout`, which an old backend does not have; a 404 is treated as a definitive refusal and the sale fails.
4. Roll the client out **when a terminal is idle** (no sale in progress); the local SQLite schema upgrades on first launch, and the first launch after login rebuilds the in-memory state.

### Rolling back

- **Backend:** redeploy the previous image. The new columns are nullable and ignored by the old code, so they can stay.
- **Database:** only drop the columns if you must (`Down` on both migrations does this). Dropping `ZReadingNumber` loses the "already reported" marks; the old code would go back to number ranges.
- **Client:** an older Flutter build still works against the new backend. Sales already queued offline on a terminal stay in that terminal's local database; **do not uninstall or clear app data** on a terminal with waiting sales.

## Verify after deploy

Run each of these against production (or staging) with a test tenant:

1. **Startup:** logs show migrations applied (or none pending) and no `Automatic migration failed`.
2. **Items / Dashboard load** — the original "unable to reach the database" symptom is gone. If a real SQL error remains it now surfaces as a 500 with the actual cause in the server log, not a 503.
3. **Online sale:** ring up an item, pay cash → receipt appears, the sale is in reports, stock decreased, a `Sale` movement exists.
4. **Retry safety:** send the same `saleId` twice (e.g. with `curl`) → second call returns the same transaction, one payment.
5. **Offline sale:** disable the terminal's network, ring up and pay cash → receipt prints marked *Saved offline*; banner shows "1 sale saved offline". Re-enable the network → within ~60 s the banner clears and the sale appears in reports **dated when it was rung up**.
6. **Z-reading ordering:** while a sale is queued offline, try a Z-reading → it is refused with a message naming the waiting sales; after sync it runs.
7. **Tenant isolation:** log in as tenant A, log out, log in as tenant B on the same terminal → no tenant A data visible. If tenant A had waiting offline sales, the logout dialog warned about them.
8. **Re-login problem:** leave a session open past 30 minutes (access-token lifetime) and confirm it keeps working without a login prompt. Watch `/auth/refresh` responses in the logs for 401s.

## Discount rules changed: Senior/PWD and promotions no longer stack

Philippine rules (RA 9994) do not let the Senior Citizen/PWD 20% discount combine with promotions or promo
codes, and only one promotion applies at a time. Previously the app stacked all of them. Now:

- The cashier chooses via the Senior/PWD switch (Admin/Manager only, as before). While it is on, **every
  promotion is suppressed** and 20% is taken off the **regular** (pre-promo) subtotal.
- Otherwise **one** promotion applies: the automatic item promos (BOGO / combo / item discount) **or** the
  promo code, whichever is larger (a tie goes to the item promos). A second promo code replaces the first.
- A promo code that isn't discounting stays on the cart, so switching Senior/PWD off restores it. The cashier
  sees what each option would save, and why a code is not applied.
- **No migration, no data change.** Completed sales are untouched; an open cart recalculates on its next edit.
- **Ship the server and the Flutter client together.** An older client still stacks discounts locally, so for a
  Senior/PWD + promo cart its total is lower than the server's, and the one-call checkout answers 409 "Prices or
  promos changed" until that terminal is updated. (Offline sales are recorded at the server's price regardless.)
- Also fixed: the customer-facing display double-counted a promo code's discount and omitted item promos, and the
  thermal receipt printed a duplicate "Discount" line for a promo code and no line for item promos.
- Not changed: the BIR reading's `GrossSales` (= net + discounts) still leaves out item-promo discounts, and the
  Senior/PWD VAT-exemption treatment is still best-effort. Whether "20% of the regular subtotal, whole cart" is
  what your accountant expects (e.g. for senior-only items or mixed groups) is worth confirming.

## Known limitations (unchanged by this deploy)

- **Offline sales can be refused by the server** for good (e.g. an item deleted while offline). The customer already has a receipt; the sale then shows as *needs review* on that terminal only. Nothing alerts a manager elsewhere.
- **A lost or reset terminal loses its unsynced sales.** They exist only on that device until synced. Do not clear app data on a terminal with waiting sales.
- **A number missing from one reading is not repeated** on later readings; a late-arriving sale is reported (and labelled late) in the next reading after it arrives, not the day it was rung up.
- **Failed checkout attempts leave voided carts**, which count in the voided total on a reading.
- **Any cashier can void an unsent local cart** (it was Admin/Manager-only when it lived on the server). Nothing had been recorded yet.
- **BIR compliance is unconfirmed.** Per-terminal device-issued receipt numbers, late-synced sales in later readings, and a `ZReadingNumber` marker are design decisions to review with your accountant before relying on them for BIR reporting.
- Credit (utang) and gateway payments cannot be completed offline.

## Files worth reviewing

- `backend/src/Purch.Application/Pos/TransactionService.cs` — `CheckoutAsync`, `RecordPaymentCoreAsync`
- `backend/src/Purch.Application/Reporting/BirReadingService.cs`
- `backend/src/Purch.Application/Auth/RefreshTokenService.cs`, `TokenRefreshService.cs`
- `backend/src/Purch.Api/Program.cs` (startup migration), `ErrorHandling/GlobalExceptionHandler.cs`
- `backend/src/Purch.Infrastructure/Persistence/Migrations/2026092100000*`
- `client/lib/features/pos/data/local_first_pos_repository.dart`, `sale_sync_coordinator.dart`
