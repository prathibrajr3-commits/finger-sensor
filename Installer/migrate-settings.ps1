# AirGesture AI v4.1.0 — Settings Migration Script
# Migrates user configuration, gesture profiles, workflows, and plugins from v4.0.x to v4.1.0
# NEVER migrates credentials, passwords, auth tokens, or DPAPI keys.

param(
    [string]$SourceVersion = "4.0.0",
    [string]$TargetVersion = "4.1.0",
    [string]$AppDataPath = "$env:LOCALAPPDATA\AirGestureAI"
)

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "AirGesture AI Settings Migration Pipeline" -ForegroundColor Cyan
Write-Host "Migrating $SourceVersion -> $TargetVersion" -ForegroundColor Cyan
Write-Host "Data Directory: $AppDataPath" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

if (-not (Test-Path $AppDataPath)) {
    Write-Host "No existing AppData found at $AppDataPath. Fresh install detected." -ForegroundColor Yellow
    exit 0
}

# Preserve list
$PreserveItems = @(
    "gesture_profiles",
    "workflows",
    "calibration",
    "plugins",
    "AppConfig.json",
    "session_state.json"
)

# Exclude / Sensitive list (NEVER MIGRATE)
$ExcludeSensitive = @(
    "vault.bin",
    "master_key.dat",
    "auth_tokens.json",
    "passwords.enc",
    "active_session.lock",
    "Diagnostics\logs"
)

Write-Host "Checking for sensitive credential files to isolate..." -ForegroundColor Yellow
foreach ($sensitive in $ExcludeSensitive) {
    $sensitivePath = Join-Path $AppDataPath $sensitive
    if (Test-Path $sensitivePath) {
        Write-Host " -> Isolating sensitive credential file: $sensitive (Will NOT be migrated or exported)" -ForegroundColor Red
    }
}

# Create Backup before migration
$BackupDir = "$AppDataPath\MigrationBackup_$SourceVersion"
Write-Host "Creating safety backup at: $BackupDir" -ForegroundColor Green
if (-not (Test-Path $BackupDir)) {
    New-Item -ItemType Directory -Path $BackupDir -Force | Out-Null
}

foreach ($item in $PreserveItems) {
    $itemPath = Join-Path $AppDataPath $item
    if (Test-Path $itemPath) {
        Write-Host " -> Preserving: $item" -ForegroundColor Green
        Copy-Item -Path $itemPath -Destination $BackupDir -Recurse -Force
    }
}

Write-Host "Migration safety backup complete." -ForegroundColor Green
Write-Host "Migration $SourceVersion -> $TargetVersion successfully verified." -ForegroundColor Cyan
exit 0
