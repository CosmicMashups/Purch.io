#Requires -Version 7.0
<#
.SYNOPSIS
    Backs up a Local-mode Purch.io installation: the Postgres database (pg_dump, custom format) and the
    uploads directory (LOCAL_STORAGE_PATH), into one timestamped folder. Also prunes backups older than
    -RetentionDays, so an unattended scheduled run does not fill the disk.

.DESCRIPTION
    Local mode has no managed backup service behind it (unlike Cloud mode's Supabase, which takes its own
    daily backups/PITR — see docs/BACKUPS.md) — this script exists to give an on-prem install the same
    safety net. Reads LOCAL_DB_CONNECTION_STRING and LOCAL_STORAGE_PATH the same way the app itself does
    (environment variables, or a -EnvFile of KEY=VALUE lines), so it stays correct if either changes.

.PARAMETER BackupRoot
    Where dated backup folders are created. Defaults to .\backups next to this script.

.PARAMETER RetentionDays
    Backup folders older than this are deleted after a successful run. Default 30. Pass 0 to keep everything.

.PARAMETER EnvFile
    Optional path to a file of KEY=VALUE lines (e.g. the same one the app's process manager loads) to read
    LOCAL_DB_CONNECTION_STRING / LOCAL_STORAGE_PATH from, when they aren't already in the environment.

.EXAMPLE
    .\backup-local.ps1
    Reads settings from the current environment and backs up into .\backups\2026-09-23_120000\.

.EXAMPLE
    .\backup-local.ps1 -EnvFile C:\Purch\.env.local -BackupRoot D:\PurchBackups -RetentionDays 90
    For a scheduled task: point at the install's env file and an external backup drive.
#>
param(
    [string]$BackupRoot = (Join-Path $PSScriptRoot 'backups'),
    [int]$RetentionDays = 30,
    [string]$EnvFile
)

$ErrorActionPreference = 'Stop'

function Read-EnvFile([string]$Path) {
    if (-not $Path) { return }
    if (-not (Test-Path $Path)) {
        throw "EnvFile not found: $Path"
    }
    foreach ($line in Get-Content $Path) {
        if ($line -match '^\s*#' -or $line -notmatch '=') { continue }
        $key, $value = $line -split '=', 2
        $key = $key.Trim()
        if (-not [System.Environment]::GetEnvironmentVariable($key)) {
            [System.Environment]::SetEnvironmentVariable($key, $value.Trim())
        }
    }
}

function Parse-NpgsqlConnectionString([string]$ConnectionString) {
    # Npgsql accepts Host/Server, Port, Database, Username/User Id, Password interchangeably; normalize to one shape.
    $parts = @{}
    foreach ($pair in $ConnectionString -split ';') {
        if (-not $pair.Trim()) { continue }
        $key, $value = $pair -split '=', 2
        $parts[$key.Trim().ToLowerInvariant()] = $value.Trim()
    }
    [PSCustomObject]@{
        Host     = $parts['host'] ?? $parts['server'] ?? 'localhost'
        Port     = $parts['port'] ?? '5432'
        Database = $parts['database'] ?? $parts['db']
        Username = $parts['username'] ?? $parts['user id'] ?? $parts['uid']
        Password = $parts['password'] ?? $parts['pwd']
    }
}

Read-EnvFile $EnvFile

$connectionString = $env:LOCAL_DB_CONNECTION_STRING
if (-not $connectionString) {
    throw 'LOCAL_DB_CONNECTION_STRING is not set (in the environment or -EnvFile). This script only backs up a Local-mode install.'
}
$storagePath = $env:LOCAL_STORAGE_PATH

$pgDump = Get-Command pg_dump -ErrorAction SilentlyContinue
if (-not $pgDump) {
    throw "pg_dump was not found on PATH. Install the PostgreSQL client tools (the same major version as the server) and retry."
}

$db = Parse-NpgsqlConnectionString $connectionString
if (-not $db.Database) {
    throw "Could not find a database name in LOCAL_DB_CONNECTION_STRING."
}

$stamp = Get-Date -Format 'yyyy-MM-dd_HHmmss'
$destination = Join-Path $BackupRoot $stamp
New-Item -ItemType Directory -Path $destination -Force | Out-Null

$dumpFile = Join-Path $destination 'database.dump'
Write-Host "Backing up database '$($db.Database)' on $($db.Host):$($db.Port) to $dumpFile ..."

$env:PGPASSWORD = $db.Password
try {
    # -Fc (custom format) is compressed and restorable with pg_restore --clean, including selectively.
    & $pgDump.Path --host=$db.Host --port=$db.Port --username=$db.Username --dbname=$db.Database --format=custom --file=$dumpFile
    if ($LASTEXITCODE -ne 0) {
        throw "pg_dump exited with code $LASTEXITCODE."
    }
}
finally {
    Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
}

if ($storagePath -and (Test-Path $storagePath)) {
    $uploadsDestination = Join-Path $destination 'uploads'
    Write-Host "Copying uploads from $storagePath to $uploadsDestination ..."
    Copy-Item -Path $storagePath -Destination $uploadsDestination -Recurse -Force
}
else {
    Write-Warning "LOCAL_STORAGE_PATH is not set or does not exist; skipping uploads backup. Receipts/branding images already on disk will not be captured."
}

Write-Host "Backup complete: $destination"

if ($RetentionDays -gt 0 -and (Test-Path $BackupRoot)) {
    $cutoff = (Get-Date).AddDays(-$RetentionDays)
    Get-ChildItem -Path $BackupRoot -Directory | Where-Object { $_.CreationTime -lt $cutoff } | ForEach-Object {
        Write-Host "Pruning backup older than $RetentionDays day(s): $($_.FullName)"
        Remove-Item $_.FullName -Recurse -Force
    }
}
