#Requires -Version 7.0
<#
.SYNOPSIS
    Restores a Local-mode Purch.io backup produced by backup-local.ps1: the database (pg_restore --clean)
    and, if present in the backup, the uploads directory.

.DESCRIPTION
    Destructive: pg_restore --clean drops existing objects before recreating them, so this overwrites the
    target database. Requires typing the database name to confirm, unless -Force is passed (for a scripted
    restore drill). Always restore into a throwaway database first to verify a backup is actually usable —
    see docs/BACKUPS.md's restore-drill section — before ever pointing this at production data.

.PARAMETER BackupFolder
    A folder created by backup-local.ps1 (containing database.dump, and optionally an uploads\ subfolder).

.PARAMETER EnvFile
    Optional path to a file of KEY=VALUE lines to read LOCAL_DB_CONNECTION_STRING / LOCAL_STORAGE_PATH from.

.PARAMETER Force
    Skip the interactive confirmation. Use only in an unattended/CI restore-drill run.

.EXAMPLE
    .\restore-local.ps1 -BackupFolder .\backups\2026-09-23_120000
#>
param(
    [Parameter(Mandatory = $true)][string]$BackupFolder,
    [string]$EnvFile,
    [switch]$Force
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

$dumpFile = Join-Path $BackupFolder 'database.dump'
if (-not (Test-Path $dumpFile)) {
    throw "No database.dump found in $BackupFolder — is this a folder produced by backup-local.ps1?"
}

$connectionString = $env:LOCAL_DB_CONNECTION_STRING
if (-not $connectionString) {
    throw 'LOCAL_DB_CONNECTION_STRING is not set (in the environment or -EnvFile).'
}
$storagePath = $env:LOCAL_STORAGE_PATH

$pgRestore = Get-Command pg_restore -ErrorAction SilentlyContinue
if (-not $pgRestore) {
    throw "pg_restore was not found on PATH. Install the PostgreSQL client tools and retry."
}

$db = Parse-NpgsqlConnectionString $connectionString
if (-not $db.Database) {
    throw "Could not find a database name in LOCAL_DB_CONNECTION_STRING."
}

if (-not $Force) {
    Write-Warning "This will DROP AND RECREATE every object in database '$($db.Database)' on $($db.Host):$($db.Port)."
    $typed = Read-Host "Type the database name to confirm"
    if ($typed -ne $db.Database) {
        throw "Confirmation did not match '$($db.Database)'. Aborted — nothing was changed."
    }
}

$env:PGPASSWORD = $db.Password
try {
    Write-Host "Restoring $dumpFile into '$($db.Database)' ..."
    & $pgRestore.Path --host=$db.Host --port=$db.Port --username=$db.Username --dbname=$db.Database --clean --if-exists --no-owner $dumpFile
    if ($LASTEXITCODE -ne 0) {
        throw "pg_restore exited with code $LASTEXITCODE. Some objects may not have restored — check the output above."
    }
}
finally {
    Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
}

$uploadsSource = Join-Path $BackupFolder 'uploads'
if (Test-Path $uploadsSource) {
    if (-not $storagePath) {
        Write-Warning "The backup includes uploads, but LOCAL_STORAGE_PATH is not set — skipping. Copy $uploadsSource manually if needed."
    }
    else {
        Write-Host "Restoring uploads to $storagePath ..."
        New-Item -ItemType Directory -Path $storagePath -Force | Out-Null
        Copy-Item -Path (Join-Path $uploadsSource '*') -Destination $storagePath -Recurse -Force
    }
}

Write-Host "Restore complete. Run 'dotnet Purch.Api.dll migrate' if this backup predates a since-added migration."
