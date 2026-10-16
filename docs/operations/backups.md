# Backups, restore and archival runbook

Status: Living · Last substantive change: 2026-10-16 (merged from two 2026-09-23 runbooks) · Audience: operators

Purch.io's two deployment modes back up differently (`PURCH_DEPLOYMENT_MODE`, see the root
[README](../../README.md#deployment-modes) and [architecture overview §3](../architecture/overview.md#3-deployment-modes)).
Part A is what each mode does today with what is in this repo. Part B is the general PostgreSQL runbook (regulatory
context, target tiering, `pg_dump` / drill procedure, archive inspection).

# Part A: per deployment mode

## Cloud mode (Supabase)

The database is a managed Supabase Postgres instance. Supabase takes its own automatic daily backups, and
paid plans add point-in-time recovery (PITR); see the Backups page in the Supabase dashboard for the
project's actual retention window and whether PITR is enabled. This app does not configure or duplicate
either. Uploaded files (`IFileStorage` → `SupabaseFileStorage`) live in Supabase Storage, which is backed by
the same project and covered by the same plan-level guarantees.

**What is not covered automatically:** a manual `pg_dump` before a risky migration, and an export you keep
outside the Supabase project entirely (e.g. before downgrading a plan, or for a tenant leaving the service).
Supabase's CLI (`supabase db dump`) or the dashboard's SQL editor can produce one; there is no script for this
here since it depends on the project's own CLI/service-role credentials, which shouldn't be scripted into this repo.

## Local mode (on-prem)

Local mode has no managed backup service behind it. [`backend/scripts/backup-local.ps1`](../../backend/scripts/backup-local.ps1)
gives an on-prem install the same safety net. It:

1. Reads `LOCAL_DB_CONNECTION_STRING` and `LOCAL_STORAGE_PATH` from the environment (or a `-EnvFile` of
   `KEY=VALUE` lines, if the install keeps them in a file the app's process manager loads).
2. Runs `pg_dump` in custom format (compressed, selectively restorable) into a timestamped folder.
3. Copies the uploads directory (receipts' source images, branding logos) alongside it.
4. Prunes backup folders older than `-RetentionDays` (default 30).

Requires the PostgreSQL client tools (`pg_dump`/`pg_restore`) on PATH, matching the server's major version.
Run from `backend/`:

```powershell
# One-off, using the current environment:
.\scripts\backup-local.ps1

# Scheduled (Task Scheduler / cron under WSL), pointed at the install's env file and an external drive:
.\scripts\backup-local.ps1 -EnvFile C:\Purch\.env.local -BackupRoot D:\PurchBackups -RetentionDays 90
```

**Schedule it.** A backup that only ever runs by hand is a backup that stops existing the week everyone is
busy. Register `backup-local.ps1` as a daily Scheduled Task on the machine running the Local-mode instance.

### Restoring

[`backend/scripts/restore-local.ps1`](../../backend/scripts/restore-local.ps1) takes a backup folder, confirms
interactively (type the database name; `-Force` skips this for a scripted drill), then runs `pg_restore --clean` and
copies back any uploads. It is destructive (it drops and recreates the target database's objects):

```powershell
.\scripts\restore-local.ps1 -BackupFolder .\backups\2026-09-23_120000
```

### Restore drills (Local mode)

A backup nobody has ever restored is a hope, not a backup. Periodically:

1. Point `LOCAL_DB_CONNECTION_STRING` at a throwaway database (not production).
2. Run `restore-local.ps1 -Force` against a recent backup.
3. Start the API against that database and confirm login, the catalog and a report load.
4. Delete the throwaway database.

## Retention vs. backups

These are different concerns. Backups protect against losing data outright (a failed disk, a bad migration, an
accidental drop). [`RetentionOptions`](../../backend/src/Purch.Infrastructure/Retention/RetentionOptions.cs) governs how
long routine rows (expired tokens, old sync idempotency records) are kept in the live database before being purged on
purpose. A backup still captures whatever the retention sweep has not yet purged. Archival of swept rows is in Part B §5.

# Part B: general PostgreSQL runbook


---

## 1. Regulatory Context (BIR & DPA Compliance)

- **National Internal Revenue Code (NIRC) §235 & Revenue Memorandum Order (RMO) No. 10-2005**:
  All books of accounts and accounting records must be preserved for at least **10 years** from the deadline of filing the return. Electronic sales records, Z-readings, and audit logs are part of this requirement.
- **Data Privacy Act of 2012 (RA 10173)**:
  Personal identifiers can be erased upon legitimate request via `POST /credit-ledger/{id}/anonymize`. Financial ledger rows remain immutable with anonymized customer references.
- **Cold Storage Archival**:
  The `RetentionSweeper` background service exports audit logs and inventory movements to compressed JSON (`.json.gz`) archives before purging records from hot operational tables when `Retention:ArchiveDirectory` is configured.

---

## 2. Backup Strategy

Purch.io uses a tiered backup strategy:

Target tiering. Nothing in this repo provisions WAL archiving, object storage or the monthly dump; treat this table as
the intended strategy, not a description of a running setup.

| Tier | Method | Frequency | Retention | Target |
| :--- | :--- | :--- | :--- | :--- |
| **Hot Backups** | Continuous WAL Archiving / PITR | Continuous | 7–30 days | S3 / Cloud Storage |
| **Daily Snapshots** | `pg_dump` (Custom format `-Fc`) | Every 24 hours | 90 days | Encrypted Object Storage |
| **Monthly Compliance**| Compressed full database dump | 1st of month | 10 years | Cold Storage / Glacier |
| **Audit Logs Archive** | Pre-retention `.json.gz` sweep | Every 6 hours | 10 years | Dedicated Archive Volume |

---

## 3. Daily Backup Procedure (`pg_dump`)

### Reference dump script

This script is not in the repository (there is no `scripts/backup_db.sh`); Local mode ships `backup-local.ps1` (Part A).
Use this as a template for a Linux host.

```bash
#!/usr/bin/env bash
set -euo pipefail

TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
BACKUP_DIR="${BACKUP_DIR:-/var/backups/purch}"
BACKUP_FILE="${BACKUP_DIR}/purch_db_${TIMESTAMP}.dump"

mkdir -p "${BACKUP_DIR}"

echo "Starting PostgreSQL backup: ${BACKUP_FILE}..."

PGPASSWORD="${DB_PASSWORD}" pg_dump \
  -h "${DB_HOST:-localhost}" \
  -p "${DB_PORT:-5432}" \
  -U "${DB_USER:-purch_app}" \
  -d "${DB_NAME:-purch_production}" \
  -Fc \
  -Z 6 \
  -f "${BACKUP_FILE}"

echo "Backup completed successfully. Size: $(du -h "${BACKUP_FILE}" | cut -f1)"

# Check integrity of the dump file header
pg_restore -l "${BACKUP_FILE}" > /dev/null
echo "Dump header verified successfully."
```

---

## 4. Restore & Drill Verification Procedure (`pg_restore`)

Run this drill quarterly in a staging or isolated Docker container to ensure backup viability.

### Step 1: Spin up verification container
```bash
docker run -d --name purch-drill-db \
  -e POSTGRES_DB=purch_drill \
  -e POSTGRES_USER=purch_app \
  -e POSTGRES_PASSWORD=drill_password \
  -p 5433:5432 \
  postgres:16-alpine
```

### Step 2: Restore dump to drill database
```bash
PGPASSWORD="drill_password" pg_restore \
  -h localhost \
  -p 5433 \
  -U purch_app \
  -d purch_drill \
  --clean \
  --if-exists \
  --no-owner \
  "${BACKUP_FILE}"
```

### Step 3: Run sanity integrity checks
```sql
-- Connect via psql
psql -h localhost -p 5433 -U purch_app -d purch_drill

-- 1. Check migrations applied
SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 5;

-- 2. Verify row counts in key tables
SELECT count(*) AS tenant_count FROM "Tenants";
SELECT count(*) AS branch_count FROM "Branches";
SELECT count(*) AS transaction_count FROM "Transactions";
SELECT count(*) AS audit_log_count FROM "AuditLogs";

-- 3. Verify Z-reading receipt sequential continuity
SELECT "DeviceId", MAX("LastIssuedNumber") FROM "ReceiptSequences" GROUP BY "DeviceId";
```

### Step 4: Tear down drill environment
```bash
docker stop purch-drill-db && docker rm purch-drill-db
```

---

## 5. Retention Sweeper Archival Inspection

When the backend runs with `Retention:ArchiveDirectory` configured:
- Audit log archives are saved as:
  `<ArchiveDirectory>/audit_logs_YYYYMMDD_HHMMSS_<Guid>.json.gz`
- Inventory movement archives are saved as:
  `<ArchiveDirectory>/inventory_movements_YYYYMMDD_HHMMSS_<Guid>.json.gz`

To inspect an archive file:
```bash
zcat <ArchiveDirectory>/audit_logs_*.json.gz | jq '.[0]'
```
