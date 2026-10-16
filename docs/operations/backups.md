# Database Backup, Disaster Recovery & Archival Runbook

This document defines the operational backup, restore, and archival procedures for Purch.io databases running PostgreSQL.

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

| Tier | Method | Frequency | Retention | Target |
| :--- | :--- | :--- | :--- | :--- |
| **Hot Backups** | Continuous WAL Archiving / PITR | Continuous | 7–30 days | S3 / Cloud Storage |
| **Daily Snapshots** | `pg_dump` (Custom format `-Fc`) | Every 24 hours | 90 days | Encrypted Object Storage |
| **Monthly Compliance**| Compressed full database dump | 1st of month | 10 years | Cold Storage / Glacier |
| **Audit Logs Archive** | Pre-retention `.json.gz` sweep | Every 6 hours | 10 years | Dedicated Archive Volume |

---

## 3. Daily Backup Procedure (`pg_dump`)

### Automated Dump Script (`scripts/backup_db.sh`)

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
