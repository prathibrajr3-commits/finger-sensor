# AirGesture AI v4.1.0 — Checksum Generator
# Hashes all distributable artifacts in dist/ and outputs dist/checksums.sha256

param(
    [string]$DistDir = "$PSScriptRoot\..\dist"
)

$DistDir = [System.IO.Path]::GetFullPath($DistDir)
Write-Host "Generating SHA-256 checksums for artifacts in '$DistDir'..." -ForegroundColor Cyan

if (-not (Test-Path $DistDir)) {
    Write-Error "Dist directory '$DistDir' does not exist."
    exit 1
}

$ChecksumFile = Join-Path $DistDir "checksums.sha256"
$Artifacts = Get-ChildItem -Path $DistDir -File | Where-Object { $_.Name -ne "checksums.sha256" }

$ChecksumLines = @()

foreach ($artifact in $Artifacts) {
    $hash = (Get-FileHash -Path $artifact.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    $line = "$hash  $($artifact.Name)"
    $ChecksumLines += $line
    Write-Host " -> $hash  $($artifact.Name)" -ForegroundColor Green
}

$ChecksumLines | Set-Content -Path $ChecksumFile -Encoding utf8
Write-Host "Checksum file created at: $ChecksumFile" -ForegroundColor Cyan
