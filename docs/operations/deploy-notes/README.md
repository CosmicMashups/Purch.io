# Deploy notes

Status: Living · Last substantive change: 2026-10-16 · Audience: operators

One note per shipped change set that needs deploy steps, oldest first. Read the note for every unit you are deploying.

| Date (git) | Note | Covers |
|---|---|---|
| 2026-09-20 | [checkout-and-offline-sales.md](checkout-and-offline-sales.md) | One-call checkout, Flutter offline sales, Z-reading fix (moved from the backend folder; see the history index) |
| 2026-10-15 | [u1-pos-foundations.md](u1-pos-foundations.md) | U1: parked carts, split tender, manual discount / price override, open items, cost and VAT stamping |
| 2026-10-15 | [u4-reports.md](u4-reports.md) | U4: reporting endpoints and two indexes |
| 2026-10-16 | [u3-inventory-depth.md](u3-inventory-depth.md) | U3: valuation, COGS, stock-take, CSV import/export |

## Checklist common to these notes

Only what the notes themselves say; each note has the specifics.

1. **Run migrations out of band, before the new code takes traffic.** On Vercel the API auto-migrates at cold start,
   but a failure is logged and non-fatal (`Automatic migration failed; ...`), so the app can serve against a stale
   schema. Use [`scripts/migrate-production.sh`](../../../scripts/migrate-production.sh) (the checkout note also names a `.ps1`
   variant, which is not in the repo) through the direct or session-mode Supabase
   connection, not the transaction pooler, and confirm in `__EFMigrationsHistory`.
2. **Run the note's pre-flight checks** (for example U1's duplicate-payment query) and take a database backup first
   ([../backups.md](../backups.md)).
3. **Row level security:** new tables enable RLS in their own migration (no policy, repo convention), covered by
   `RowLevelSecurityCoverageTests`. Nothing to do by hand.
4. **Order:** database, then backend, then clients. A new client against an old backend fails.
5. **Flutter:** check the note's Flutter caveat. U3 and U4 have no Flutter change; U1's unchanged Flutter client does
   not know the new states.
6. **Rollback:** read the note's rollback section before deploying; some `Down` migrations fail or lose data.
7. **Smoke-check** with the note's post-deploy list.
