# AirGesture AI v4.1.0 — Settings & Profile Migration Guide

This guide details how user configurations, gesture profiles, workflows, and session data migrate from **v4.0.x** to **v4.1.0**.

---

## 1. Automated Settings Migration

When AirGesture AI v4.1.0 launches for the first time or when running `Installer/migrate-settings.ps1`, the migration engine inspects `%LocalAppData%\AirGestureAI`.

### Preserved Assets
The following user files are automatically backed up and migrated to v4.1.0 format:

- **Gesture Profiles**: `gesture_profiles/*.json`
- **Workflows**: `workflows/*.json`
- **Stereo Lens Calibration**: `calibration/stereo_calib.json`
- **Plugin Configurations**: `plugins/manifests/` & settings
- **User Preferences**: `AppConfig.json`
- **Session State**: `session_state.json`

### Excluded / Isolated Assets (Never Migrated)
For security and tamper protection, credentials and tokens are **never** migrated across environments or exported:

- `vault.bin` (DPAPI encrypted credentials)
- `master_key.dat`
- `auth_tokens.json`
- `active_session.lock` (recreated cleanly on startup)

---

## 2. Command Line Migration Script

To run migration manually:

```powershell
powershell -ExecutionPolicy Bypass -File .\Installer\migrate-settings.ps1 -SourceVersion "4.0.0" -TargetVersion "4.1.0"
```

The script automatically creates a timestamped safety backup in `%LocalAppData%\AirGestureAI\MigrationBackup_4.0.0` before applying changes.
