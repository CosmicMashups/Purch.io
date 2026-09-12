# No-Docker alternative, step 2 of 2: registers the published Purch.Api.exe
# (see publish-self-contained.ps1) as a Windows Service, so it starts
# automatically on boot and keeps running without anyone staying logged in.
# Requires a Postgres instance already installed and running on this machine
# (the official Windows installer from postgresql.org) — this script does
# not bundle one; see README.md.
#
# Usage (run as Administrator, from this installer/ directory):
#   .\install-windows-service.ps1 `
#       -DbConnectionString "Host=localhost;Database=purch;Username=purch;Password=..." `
#       -JwtSigningKey "<a long random string>" `
#       -StoragePath "C:\ProgramData\Purch\storage" `
#       -Port 8080

param(
    [Parameter(Mandatory = $true)]
    [string]$DbConnectionString,

    [Parameter(Mandatory = $true)]
    [string]$JwtSigningKey,

    [string]$StoragePath = "C:\ProgramData\Purch\storage",

    [int]$Port = 8080,

    [string]$ServiceName = "PurchIoBackend",

    [string]$ExePath = (Join-Path $PSScriptRoot "dist\Purch.Api.exe")
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $ExePath)) {
    throw "$ExePath not found — run publish-self-contained.ps1 first."
}

New-Item -ItemType Directory -Force -Path $StoragePath | Out-Null

# ASP.NET Core reads configuration from machine-level environment variables
# automatically — a Windows Service has no .env file to load, so this is the
# equivalent of docker-compose.local.yml's `environment:` block for this path.
[Environment]::SetEnvironmentVariable("PURCH_DEPLOYMENT_MODE", "Local", "Machine")
[Environment]::SetEnvironmentVariable("LOCAL_DB_CONNECTION_STRING", $DbConnectionString, "Machine")
[Environment]::SetEnvironmentVariable("LOCAL_STORAGE_PATH", $StoragePath, "Machine")
[Environment]::SetEnvironmentVariable("JWT_SIGNING_KEY", $JwtSigningKey, "Machine")
[Environment]::SetEnvironmentVariable("JWT_ISSUER", "purch.io", "Machine")
[Environment]::SetEnvironmentVariable("ASPNETCORE_URLS", "http://+:$Port", "Machine")

if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
    Write-Host "Service '$ServiceName' already exists — stopping it to reinstall." -ForegroundColor Yellow
    Stop-Service -Name $ServiceName -Force
    sc.exe delete $ServiceName | Out-Null
}

New-Service -Name $ServiceName `
    -BinaryPathName $ExePath `
    -DisplayName "Purch.io Backend (Local)" `
    -StartupType Automatic

Write-Host "Opening firewall port $Port for LAN devices..." -ForegroundColor Cyan
New-NetFirewallRule -DisplayName "Purch.io Backend ($Port)" `
    -Direction Inbound -Protocol TCP -LocalPort $Port -Action Allow `
    -ErrorAction SilentlyContinue | Out-Null

Start-Service -Name $ServiceName

Write-Host ""
Write-Host "Done — '$ServiceName' is running and will start automatically on boot." -ForegroundColor Green
Write-Host "Find this machine's LAN IP (ipconfig) and enter http://<that-ip>:$Port on each POS device's 'Connect to a local server' screen."
