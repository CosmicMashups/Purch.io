# No-Docker alternative, step 1 of 2: publishes the backend as a
# self-contained, single-file Windows executable — no .NET runtime install
# needed on the target machine. Run install-windows-service.ps1 next.
#
# Usage (from this installer/ directory):
#   .\publish-self-contained.ps1

$ErrorActionPreference = "Stop"

$backendDir = Join-Path $PSScriptRoot "..\backend\src\Purch.Api"
$outputDir = Join-Path $PSScriptRoot "dist"

Write-Host "Publishing a self-contained win-x64 build to $outputDir ..." -ForegroundColor Cyan

# Uses dotnet's full install path rather than the bare `dotnet` command,
# which can resolve to an unrelated stub depending on this machine's PATH.
$dotnetExe = "C:\Program Files\dotnet\dotnet.exe"
if (-not (Test-Path $dotnetExe)) {
    $dotnetExe = "dotnet"
}

& $dotnetExe publish $backendDir `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $outputDir `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true

Write-Host ""
Write-Host "Done — Purch.Api.exe is in $outputDir." -ForegroundColor Green
Write-Host "Next: copy .env.local.template to a config file (see README.md) and run install-windows-service.ps1."
