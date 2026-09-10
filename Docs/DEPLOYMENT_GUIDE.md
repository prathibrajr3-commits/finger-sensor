# AirGesture AI v4.1.0 — Deployment & Distribution Guide

This guide details the deployment options, installation mechanisms, silent deployment commands, and artifact verification process for **AirGesture AI v4.1.0 RTM**.

---

## 1. Overview of Deployment Packages

AirGesture AI v4.1.0 is distributed in two main deployment formats:

| Format | Artifact Name | Target Audience | Installation Scope |
|---|---|---|---|
| **Portable ZIP** | `AirGestureAI-4.1.0-Portable.zip` | Developers, non-admin users, USB deployment | User folder / Standalone |
| **WiX MSI Installer** | `AirGestureAI-4.1.0-Setup.msi` | IT administrators, enterprise rollout | Per-Machine (`C:\Program Files\AirGestureAI`) |

---

## 2. Portable Package Deployment

### Extraction & Execution
1. Download `AirGestureAI-4.1.0-Portable.zip`.
2. Verify checksum against `checksums.sha256`:
   ```powershell
   (Get-FileHash AirGestureAI-4.1.0-Portable.zip -Algorithm SHA256).Hash.ToLower()
   ```
3. Extract to desired directory (e.g., `C:\Tools\AirGestureAI`).
4. Run `AirGestureAI.exe`.

### Configuration Isolation
Portable mode automatically stores logs, user profiles, and session state inside `%LocalAppData%\AirGestureAI` unless custom arguments are specified.

---

## 3. WiX MSI Installer Deployment

### Standard Installation
Double-click `AirGestureAI-4.1.0-Setup.msi` and follow the setup wizard.

### Silent Installation (Enterprise Automation)
```cmd
msiexec /i AirGestureAI-4.1.0-Setup.msi /qn /norestart CREATEDESKTOPSHORTCUT=1
```

### Silent Uninstall
```cmd
msiexec /x AirGestureAI-4.1.0-Setup.msi /qn /norestart
```

### Repair Installation
```cmd
msiexec /f AirGestureAI-4.1.0-Setup.msi /qn
```

---

## 4. Upgrade & Data Preservation Rules

- **Major Upgrade Detection**: The installer checks UpgradeCode `8B42478D-59A6-4A9B-B12A-6A38F3B3A710` and automatically replaces previous 4.0.x installations.
- **User Data Protection**: User profiles, workflows, gesture definitions, and logs stored in `%LocalAppData%\AirGestureAI` are **preserved** during upgrades and standard uninstalls.

---

## 5. Artifact Verification & SBOM

Every official release includes:
- `checksums.sha256` for file integrity checks
- `release_manifest.json` for build provenance metadata
- `SBOM.spdx.json` containing complete Software Bill of Materials (SPDX 2.3)
- `SIGNING_NOT_CONFIGURED.md` documenting Authenticode code signing status
