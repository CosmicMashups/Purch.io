# Backups and restore

Purch.io's two deployment modes back up differently — see `PURCH_DEPLOYMENT_MODE` in the main
[README](../README.md#deployment-configuration).

## Cloud mode (Supabase)

The database is a managed Supabase Postgres instance. Supabase takes its own automatic daily backups, and
paid plans add point-in-time recovery (PITR) — see the Backups page in the Supabase dashboard for the
project's actual retention window and whether PITR is enabled; this app does not configure or duplicate
either. Uploaded files (`IFileStorage` → `SupabaseFileStorage`) live in Supabase Storage, which is backed by
the same project and covered by the same plan-level guarantees.

**What is not covered automatically:** a manual `pg_dump` before a risky migration, and an export you keep
outside the Supabase project entirely (e.g. before downgrading a plan, or for a tenant leaving the service —
see the transaction export below). Supabase's CLI (`supabase db dump`) or the dashboard's SQL editor can
produce one; there is no script for this here since it depends on the project's own CLI/service-role
credentials, which shouldn't be scripted into this repo.

## Local mode (on-prem)

Local mode has no managed backup service behind it — [`scripts/backup-local.ps1`](../scripts/backup-local.ps1)
exists to give an on-prem install the same safety net. It:

1. Reads `LOCAL_DB_CONNECTION_STRING` and `LOCAL_STORAGE_PATH` from the environment (or a `-EnvFile` of
   `KEY=VALUE` lines, if the install keeps them in a file the app's process manager loads).
2. Runs `pg_dump` in custom format (compressed, selectively restorable) into a timestamped folder.
3. Copies the uploads directory (receipts' source images, branding logos) alongside it.
4. Prunes backup folders older than `-RetentionDays` (default 30).

Requires the PostgreSQL client tools (`pg_dump`/`pg_restore`) on PATH, matching the server's major version.

```powershell
# One-off, using the current environment:
.\scripts\backup-local.ps1

# Scheduled (Task Scheduler / cron under WSL), pointed at the install's env file and an external drive:
.\scripts\backup-local.ps1 -EnvFile C:\Purch\.env.local -BackupRoot D:\PurchBackups -RetentionDays 90
```

**Schedule it.** A backup that only ever runs by hand is a backup that stops existing the week everyone is
busy. Register `backup-local.ps1` as a daily Scheduled Task on the machine running the Local-mode instance.

### Restoring

[`scripts/restore-local.ps1`](../scripts/restore-local.ps1) takes a backup folder, confirms interactively
(type the database name; `-Force` skips this for a scripted drill), then runs `pg_restore --clean` and
copies back any uploads. It is destructive — it drops and recreates the target database's objects — so:

```powershell
.\scripts\restore-local.ps1 -BackupFolder .\backups\2026-09-23_120000
```

### Restore drills

A backup nobody has ever restored is a hope, not a backup. Periodically:

1. Point `LOCAL_DB_CONNECTION_STRING` at a throwaway database (not production).
2. Run `restore-local.ps1 -Force` against a recent backup.
3. Start the API against that database and confirm login, the catalog and a report load.
4. Delete the throwaway database.

## Retention vs. backups

These are different concerns. Backups (this document) protect against losing data outright — a failed
disk, a bad migration, an accidental drop. [`RetentionOptions`](../src/Purch.Infrastructure/Retention/RetentionOptions.cs)
governs how long routine rows (expired tokens, old sync idempotency records) are kept in the live database
before being purged on purpose. A backup still captures whatever the retention sweep has not yet purged.
