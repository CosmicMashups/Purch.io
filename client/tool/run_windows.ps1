<#
.SYNOPSIS
    Runs `flutter run -d windows`, working around a Visual Studio toolset
    mismatch that breaks the flutter_secure_storage_windows plugin build.

.DESCRIPTION
    VS 2022's "v143" platform toolset resolves to whatever MSVC version
    VC\Auxiliary\Build\Microsoft.VCToolsVersion.v143.default.txt pins — which
    can lag behind the newest MSVC toolset actually installed. If the C++ ATL
    component (required by flutter_secure_storage_windows) was only installed
    against that newer toolset, the default-pinned older one has no ATL
    headers, and the build fails deep in an MSBuild log with:

        error C1083: Cannot open include file: 'atlstr.h'

    This script finds the newest installed MSVC toolset that actually has
    ATL (by checking for atlmfc\include on disk, not just assuming "newest"
    has it) and forces the build onto it via the VCToolsVersion environment
    variable, which MSBuild respects as an override of that default pin.

.EXAMPLE
    .\tool\run_windows.ps1
    .\tool\run_windows.ps1 --dart-define=PURCH_API_BASE_URL=https://localhost:5001
#>

$ErrorActionPreference = 'Stop'

$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path $vswhere)) {
    Write-Error "vswhere.exe not found — is Visual Studio 2022 installed? Expected it at: $vswhere"
    exit 1
}

$vsInstallPath = & $vswhere -latest -products * -property installationPath
if (-not $vsInstallPath) {
    Write-Error "vswhere couldn't find any Visual Studio installation."
    exit 1
}

$msvcRoot = Join-Path $vsInstallPath 'VC\Tools\MSVC'
if (-not (Test-Path $msvcRoot)) {
    Write-Error "No MSVC toolset directory found under: $msvcRoot"
    exit 1
}

# Newest-first, so the first ATL-equipped toolset we find is the newest one.
$atlToolset = Get-ChildItem $msvcRoot -Directory |
    Sort-Object { [version]$_.Name } -Descending |
    Where-Object { Test-Path (Join-Path $_.FullName 'atlmfc\include\atlstr.h') } |
    Select-Object -First 1

if (-not $atlToolset) {
    Write-Error @"
No installed MSVC toolset has the C++ ATL headers (atlmfc\include\atlstr.h).

flutter_secure_storage_windows needs ATL to build. Install it via:
  Visual Studio Installer -> Modify -> Individual components ->
  "C++ ATL for latest v143 build tools (x86 & x64)"
then re-run this script.
"@
    exit 1
}

Write-Host "Using MSVC toolset $($atlToolset.Name) (has ATL) for the Windows build." -ForegroundColor Cyan
$env:VCToolsVersion = $atlToolset.Name

flutter run -d windows @args
exit $LASTEXITCODE
