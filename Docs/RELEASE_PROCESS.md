# AirGesture AI v4.1.0 — Release Engineering Process

This document outlines the standard operating procedure for building, testing, packaging, and publishing AirGesture AI releases.

---

## 1. Release Engineering Pipeline

The release pipeline consists of 12 sequential stages executed by `Build/BuildRelease.ps1`:

1. **Clean & Restore**: Purge previous build artifacts and restore NuGet packages.
2. **Compile Release**: Build solution under `Release` configuration (`TreatWarningsAsErrors=true`).
3. **Run Test Suite**: Execute xUnit tests across all subsystems (`AirGestureAI.Tests.csproj`).
4. **Publish Binary Bundle**: Perform `dotnet publish` targeting `win-x64`.
5. **Stage Assets**: Gather application executables, Python hand tracking models, documentation, licenses, and third-party notices.
6. **Package Portable ZIP**: Archive staged files into `dist/AirGestureAI-4.1.0-Portable.zip`.
7. **Compile WiX Installer**: Generate `dist/AirGestureAI-4.1.0-Setup.msi` if `candle.exe`/`light.exe` is available.
8. **Package SDK & Docs**: Generate `dist/SDK.zip` and `dist/Documentation.zip`.
9. **Generate Release Manifest**: Write product metadata, commit hash, and artifact hashes to `dist/release_manifest.json`.
10. **Generate SBOM**: Create SPDX 2.3 bill of materials in `dist/SBOM.spdx.json`.
11. **Generate Checksums**: Compute SHA-256 digests and write `dist/checksums.sha256`.
12. **Run Release Validator**: Execute `ReleaseValidator` checks and produce `dist/release_validation_report.json`.

---

## 2. Running Build Automation Locally

To run the release pipeline locally:

```powershell
# Complete release build
.\Build\BuildRelease.ps1 -Configuration Release -RuntimeIdentifier win-x64

# Quick build skipping tests and installer
.\Build\BuildRelease.ps1 -SkipTests -SkipInstaller
```

---

## 3. Code Signing Policy

- If an Authenticode PFX certificate is present, binaries must be signed with SHA-256 timestamping before packaging.
- If code signing is not configured, the release pipeline generates `SIGNING_NOT_CONFIGURED.md` documenting that the distribution is unsigned.
- Binary signatures must be inspected via `SDK.BinarySigner.ReadAuthenticodeSignature()`.
