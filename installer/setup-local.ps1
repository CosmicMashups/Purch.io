# Local/on-prem setup (Docker path) — brings up Postgres + the Purch.io
# backend as containers on this machine, migrating the database
# automatically on first boot (see Program.cs). Requires Docker Desktop.
#
# Usage (from this installer/ directory):
#   .\setup-local.ps1
#
# See README.md for the no-Docker (Windows service) alternative.

$ErrorActionPreference = "Stop"

$envFile = Join-Path $PSScriptRoot ".env.local"
$envTemplate = Join-Path $PSScriptRoot ".env.local.template"

if (-not (Test-Path $envFile)) {
    Copy-Item $envTemplate $envFile
    Write-Host "Created .env.local from the template — fill in POSTGRES_PASSWORD and JWT_SIGNING_KEY, then re-run this script." -ForegroundColor Yellow
    exit 1
}

$envContent = Get-Content $envFile -Raw
if ($envContent -match "POSTGRES_PASSWORD=\s*(#.*)?$" -or $envContent -notmatch "JWT_SIGNING_KEY=\S") {
    Write-Host ".env.local is missing POSTGRES_PASSWORD or JWT_SIGNING_KEY — fill those in before continuing." -ForegroundColor Red
    exit 1
}

Write-Host "Starting Postgres + backend containers..." -ForegroundColor Cyan
docker compose -f (Join-Path $PSScriptRoot "docker-compose.local.yml") --env-file $envFile up -d --build

Write-Host ""
Write-Host "Done. The backend migrates its own database on first boot — check progress with:" -ForegroundColor Green
Write-Host "  docker compose -f docker-compose.local.yml logs -f backend"
Write-Host ""
Write-Host "Once it's up, find this machine's LAN IP (ipconfig) and enter http://<that-ip>:<PURCH_PORT> on each POS device's 'Connect to a local server' screen."
