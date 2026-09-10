# AirGesture AI v4.1.0 RTM — Release Notes

AirGesture AI v4.1.0 RTM represents the production-ready Release To Manufacturing build for the offline desktop gesture control platform.

---

## Highlights in v4.1.0 RTM

### 1. Crash Recovery & Session Restoration (Phase 5 & 6)
- **Lock-File Crash Detection**: Automatically detects ungraceful process terminations on startup.
- **Priority Recovery Chain**: Restores session state from primary JSON, backup snapshot, or diagnostics autosave.
- **Safe Mode**: Activates Safe Mode after 3 consecutive startup failures to prevent crash loops.
- **In-Window Recovery Status Banner**: Visual amber/red banner in MainWindow displaying restoration source and validation severity.

### 2. Packaging & Release Engineering (Phase 7)
- **Central Version Alignment**: Aligned assembly, file, and product version to `4.1.0`.
- **Portable Package**: Compressed production archive `AirGestureAI-4.1.0-Portable.zip`.
- **WiX Installer**: Enterprise MSI installer `AirGestureAI.wxs` with silent install and major upgrade prevention.
- **Software Bill of Materials**: Standardized SPDX 2.3 JSON specification (`SBOM.spdx.json`).
- **Release Manifest & Checksums**: SHA-256 integrity digest mapping and automated verification report (`release_validation_report.json`).

---

## Upgrading to v4.1.0

Existing v4.0.x configurations, gesture profiles, workflows, and calibration profiles are automatically preserved during upgrade. Passwords and DPAPI master keys are isolated and maintained securely in `%LocalAppData%\AirGestureAI`.
