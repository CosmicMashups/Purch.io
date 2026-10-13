<#
.SYNOPSIS
  Prunes old images from the Vercel Container Registry so a deploy's image push does not fail on a full registry.

.DESCRIPTION
  Lists the images in a registry repository (default "api") and removes those older than -OlderThanDays,
  always keeping the newest -KeepNewest images for rollback. DRY RUN by default: nothing is deleted unless
  you pass -Apply.

  Vercel does not report which images a live deployment uses, so check the dashboard for the current
  production deployment before running with -Apply, and keep -KeepNewest generous.

.EXAMPLE
  .\scripts\prune-vercel-images.ps1                 # show what would be deleted
  .\scripts\prune-vercel-images.ps1 -Apply          # delete images older than 7 days (keeping the newest 10)
  .\scripts\prune-vercel-images.ps1 -OlderThanDays 14 -KeepNewest 15 -Apply
#>
[CmdletBinding()]
param(
    [string]$Repository = 'api',
    [int]$OlderThanDays = 7,
    [int]$KeepNewest = 10,
    [switch]$Apply
)

$ErrorActionPreference = 'Stop'

# The registry commands act on the project linked in backend/.
$backend = Join-Path $PSScriptRoot '..\backend'
Push-Location $backend
try {
    if (-not (Get-Command vercel -ErrorAction SilentlyContinue)) {
        throw 'The Vercel CLI is not installed. Run: npm i -g vercel'
    }

    & vercel whoami *> $null
    if ($LASTEXITCODE -ne 0) {
        throw 'Not logged in to Vercel. Run: vercel login'
    }

    # Rows look like: image_<id>  <digest>  <tag or <none>>  Image  Ready  amd64  59.5MB  14h ago
    $images = foreach ($entry in (& vercel vcr image ls $Repository 2>&1)) {
        $line = "$entry"  # stderr lines arrive as ErrorRecords; treat everything as text
        if ($line -match '^\s*(image_\w+)\s+.*?\s(\d+)([mhdw])\s+ago\s*$') {
            $unit = @{ m = 1 / 1440; h = 1 / 24; d = 1; w = 7 }[$Matches[3]]
            [pscustomobject]@{ Id = $Matches[1]; AgeDays = [int]$Matches[2] * $unit; Raw = $line.Trim() }
        }
    }

    if (-not $images) { throw "No images found in repository '$Repository' (or the listing format changed)." }

    $sorted = @($images | Sort-Object AgeDays)
    $protected = $sorted | Select-Object -First $KeepNewest
    $stale = @($sorted | Where-Object { $_.AgeDays -gt $OlderThanDays -and $_.Id -notin $protected.Id })

    Write-Host "$($sorted.Count) images in '$Repository'; keeping the newest $KeepNewest; $($stale.Count) older than $OlderThanDays days."
    if ($stale.Count -eq 0) { return }

    foreach ($image in $stale) { Write-Host "  $($image.Raw)" }

    if (-not $Apply) {
        Write-Host 'Dry run: nothing deleted. Re-run with -Apply to delete the images above.'
        return
    }

    foreach ($image in $stale) {
        & vercel vcr image rm $Repository $image.Id
        if ($LASTEXITCODE -ne 0) { Write-Warning "Failed to remove $($image.Id)" }
    }
}
finally {
    Pop-Location
}
