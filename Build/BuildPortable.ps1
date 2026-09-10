# AirGesture AI v4.1.0 — Portable Package Builder
# Packages published binaries and assets into dist/AirGestureAI-4.1.0-Portable.zip

param(
    [string]$PublishDir = "$PSScriptRoot\..\bin\Release\net8.0-windows\win-x64\publish",
    [string]$DistDir = "$PSScriptRoot\..\dist",
    [string]$Version = "4.1.0"
)

$ErrorActionPreference = "Stop"
$DistDir = [System.IO.Path]::GetFullPath($DistDir)
$PublishDir = [System.IO.Path]::GetFullPath($PublishDir)
$RootDir = [System.IO.Path]::GetFullPath("$PSScriptRoot\..")

Write-Host "Building Portable Package ($Version)..." -ForegroundColor Cyan
Write-Host "Publish Directory: $PublishDir" -ForegroundColor Gray
Write-Host "Dist Directory:    $DistDir" -ForegroundColor Gray

if (-not (Test-Path $PublishDir)) {
    Write-Host "Publish directory missing. Creating staging folder from project root binaries..." -ForegroundColor Yellow
    $PublishDir = "$RootDir\bin\Release\net8.0-windows"
}

if (-not (Test-Path $DistDir)) {
    New-Item -ItemType Directory -Path $DistDir -Force | Out-Null
}

$StagingDir = Join-Path $DistDir "Staging_Portable"
if (Test-Path $StagingDir) { Remove-Item -Path $StagingDir -Recurse -Force }
New-Item -ItemType Directory -Path $StagingDir -Force | Out-Null

# Copy binaries if available
if (Test-Path $PublishDir) {
    Copy-Item -Path "$PublishDir\*" -Destination $StagingDir -Recurse -Force -Exclude "*.pdb","*.xml"
}

# Copy root documentation and licensing files
$RootFilesToInclude = @(
    "LICENSE.md",
    "THIRD_PARTY_NOTICES.md",
    "README.md",
    "SIGNING_NOT_CONFIGURED.md"
)

foreach ($file in $RootFilesToInclude) {
    $src = Join-Path $RootDir $file
    if (Test-Path $src) {
        Copy-Item -Path $src -Destination $StagingDir -Force
    }
}

# Copy Docs directory
$DocsDir = Join-Path $RootDir "Docs"
if (Test-Path $DocsDir) {
    Copy-Item -Path $DocsDir -Destination (Join-Path $StagingDir "Docs") -Recurse -Force
}

# Copy Python hand tracking files if present
$HandTrackingDir = Join-Path $RootDir "HandTracking"
if (Test-Path $HandTrackingDir) {
    Copy-Item -Path $HandTrackingDir -Destination (Join-Path $StagingDir "HandTracking") -Recurse -Force -Exclude "*.pyc","__pycache__"
}

# Copy Models & Plugins directories if present
foreach ($folder in @("Models", "Plugins")) {
    $folderPath = Join-Path $RootDir $folder
    if (Test-Path $folderPath) {
        Copy-Item -Path $folderPath -Destination (Join-Path $StagingDir $folder) -Recurse -Force
    } else {
        New-Item -ItemType Directory -Path (Join-Path $StagingDir $folder) -Force | Out-Null
    }
}

# Ensure development artifacts, source files, and secrets are strictly excluded
$ExcludeList = @("*.pdb", "*.tmp", "*.cs", "*.csproj", "*.user", "vault.bin", "master_key.dat", "recovery_state.json", "active_session.lock", "logs", "obj", "bin", "Tests")
foreach ($pattern in $ExcludeList) {
    Get-ChildItem -Path "$StagingDir\*" -Recurse -Include $pattern | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
}
# Double-check specific prohibited files/folders
Get-ChildItem -Path "$StagingDir" -Recurse | Where-Object {
    $_.Name -match '\.(cs|csproj|pdb|tmp)$' -or
    $_.Name -in @("vault.bin", "master_key.dat", "recovery_state.json", "active_session.lock", "obj", "bin", "Tests")
} | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

# ── Windows x64 Runtime Pruning ─────────────────────────────────────────────
# dotnet publish includes native assets for every RID in the NuGet graph.
# This package targets Windows x64 only (WPF, net8.0-windows, win-x64 publish).
# Required: runtimes\win-x64\  (ONNX Runtime + OpenCV x64 natives)
#           runtimes\win\      (System.Management managed DLL - win RID)
# Removed:  android, ios, linux-*, osx-*, win-arm64, win-x86
$RuntimesDir = Join-Path $StagingDir "runtimes"
if (Test-Path $RuntimesDir) {
    $UnnecessaryRids = @(
        "android",
        "ios",
        "linux-arm64",
        "linux-x64",
        "osx-arm64",
        "osx-x64",
        "win-arm64",
        "win-x86"
    )
    foreach ($rid in $UnnecessaryRids) {
        $ridPath = Join-Path $RuntimesDir $rid
        if (Test-Path $ridPath) {
            $sizeMB = [math]::Round((Get-ChildItem $ridPath -Recurse -File | Measure-Object Length -Sum).Sum / 1MB, 1)
            # Retry loop: antivirus / indexer may briefly lock files during staging copy
            Remove-Item -Path $ridPath -Recurse -Force -ErrorAction SilentlyContinue
            if (Test-Path $ridPath) {
                Start-Sleep -Milliseconds 750
                Remove-Item -Path $ridPath -Recurse -Force -ErrorAction SilentlyContinue
            }
            if (Test-Path $ridPath) {
                Write-Warning "  Could not fully remove runtimes\$rid - file locked by another process. Continuing."
            } else {
                Write-Host "  Pruned runtimes\$rid ($sizeMB MB - not needed for win-x64)" -ForegroundColor DarkYellow
            }
        }
    }
    Write-Host "  Kept:  runtimes\win-x64 (ONNX Runtime + OpenCV x64 natives)" -ForegroundColor Green
    Write-Host "  Kept:  runtimes\win     (System.Management managed DLL)" -ForegroundColor Green
}

# Create Zip Archive
$ZipPath = Join-Path $DistDir "AirGestureAI-$Version-Portable.zip"
if (Test-Path $ZipPath) { Remove-Item -Path $ZipPath -Force }

Compress-Archive -Path "$StagingDir\*" -DestinationPath $ZipPath -CompressionLevel Optimal
Remove-Item -Path $StagingDir -Recurse -Force

Write-Host "Portable package successfully created at: $ZipPath" -ForegroundColor Green
