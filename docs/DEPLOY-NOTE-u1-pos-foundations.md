# Deployment note — U1 POS foundations

Parked carts, split tender, manual discount/price override, open items and line notes, cost and VAT stamping.
Read **Before you deploy** first: the new code reads the new columns on every cart, checkout and orders path, so
the migrations must be applied before the new deployment takes traffic.

## Migrations (apply in this order)

1. `20261014011918_AddU1PosFoundations` — parked-cart, split-tender, cost, VAT, manual-discount and open-item
   columns; `TransactionLines.ItemId` becomes nullable; new unique index `IX_Payments_TransactionId_Sequence`
   on `Payments (TransactionId, Sequence)`.
2. `20261014012721_BackfillTransactionVat` — one `UPDATE` stamping the VAT split on Completed (2) and Refunded (4)
   transactions where `VatRateApplied IS NULL`, marked `VatDerived = TRUE`.
3. `20261014062039_AddManualDiscountKindValue` — `ManualDiscountKind`/`ManualDiscountValue` on `Transactions` and
   `TransactionLines`. Pure add.

(The U1 work shipped three migrations, not four.)

## Before you deploy

### 1. Pre-flight duplicate check

The new unique index is on `(TransactionId, Sequence)` and `Sequence` defaults to 0 for every existing payment, so
any transaction with more than one payment row makes migration 1 fail. This must return **no rows**:

```sql
SELECT "TransactionId", count(*) FROM "Payments" GROUP BY 1 HAVING count(*) > 1;
```

If it returns rows, resolve them (renumber `Sequence` or merge) before migrating.

### 2. Run migrations out of band, before promoting

The backfill is a single `UPDATE` over all Completed/Refunded transactions (cost is proportional to the number of
completed sales; it takes row locks on those rows for its duration, so run it in a quiet period). Estimate it first:

```sql
SELECT count(*) FROM "Transactions" WHERE "Status" IN (2, 4);
```

On Vercel a migration failure at cold start is logged and non-fatal, while the new code reads the new columns on
every path. So do **not** rely on startup migration. Apply them yourself, **before** promoting the new deployment,
through the Supabase **session-mode** connection (direct, port 5432 — not the transaction pooler):

```bash
./scripts/migrate-production.sh      # prompts for SUPABASE_DB_CONNECTION_STRING (input hidden)
```

The script runs `dotnet ef database update` against `Purch.Infrastructure`/`Purch.Api`. Confirm afterwards:

```sql
SELECT "MigrationId" FROM "__EFMigrationsHistory" WHERE "MigrationId" >= '20261014' ORDER BY 1;
```

Then promote the deployment.

## Rollback caveats

- `AddU1PosFoundations` Down puts `TransactionLines.ItemId` back to NOT NULL; it **fails while open-item rows exist**.
  Delete or backfill them first.
- `BackfillTransactionVat` Down is a no-op: the stamped VAT values stay (harmless; the columns themselves are
  dropped by reverting migration 1).
- Parked carts (`Status = 5`) are unknown to the old code; discard or resume them before rolling the code back.

## Defaults

- `Tenant.OpenItemEnabled` = true (admins can switch it off via `PUT /tenant/settings/barcode`).
- `Tenant.ParkedCartTtlHours` = 24 (1..168; a parked cart older than this is expired by the sweep and voided).

## Post-deploy smoke checks

1. Park a cart, list parked, resume it on a register; discard one with a manager PIN.
2. Split a payment (cash + GCash) and confirm the receipt and `Payments.Sequence` 0/1.
3. Manager applies a line discount and a price override; a cashier gets 403.
4. Add an open item and a line note; check out; refund it.
5. Take an X reading and confirm VAT figures match the receipts; open Orders and export CSV.

## Flutter client caveat

The unchanged Flutter client has no knowledge of the new states:

- It must never receive a `Parked` transaction (status index 5 exceeds its enum). Parked carts are excluded from the
  orders lists; keep it that way until U6 ships the client.
- Open-item lines reach it with `itemId` all zeros (`00000000-0000-0000-0000-000000000000`) and the name in `itemName`.
