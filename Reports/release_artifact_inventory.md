# AirGesture AI v4.1 RTM — Release Artifact Inventory

**Target Release**: AirGesture AI v4.1.0 RTM  
**Inventory Timestamp**: 2026-08-13  

---

## 1. Source & Engineering Infrastructure Assets (`VERIFIED`)

| Asset | Path | Status |
|---|---|---|
| Main Project File | `AirGestureAI.csproj` | Present (`Version=4.1.0`) |
| Release Manager | `Release/ReleaseManager.cs` | Present (`AppVersion=4.1.0`) |
| Binary Signer Service | `SDK/BinarySigner.cs` | Present (Authenticode inspection) |
| Release Validator | `Services/ReleaseValidator.cs` | Present |
| WiX Installer Spec | `Installer/AirGestureAI.wxs` | Present (WiX v3 spec) |
| Settings Migrator | `Installer/migrate-settings.ps1` | Present |
| Checksum Generator | `Installer/generate-checksums.ps1` | Present |
| Portable Builder | `Build/BuildPortable.ps1` | Present |
| Master Release Script | `Build/BuildRelease.ps1` | Present |
| Release Test Suite | `Tests/ReleasePackagingTests.cs` | Present |
| CI/CD Pipeline | `.github/workflows/release.yml` | Present |
| Code Signing Notice | `SIGNING_NOT_CONFIGURED.md` | Present |

---

## 2. Release Distribution Artifacts (`dist/`)

| Artifact Name | Type | Status | Reason / Details |
|---|---|---|---|
| `release_manifest.json` | Metadata | **PRESENT** | Version 4.1.0 release manifest |
| `SBOM.spdx.json` | Compliance | **PRESENT** | SPDX 2.3 bill of materials |
| `checksums.sha256` | Security | **PRESENT** | SHA-256 digests of dist metadata |
| `release_validation_report.json` | QA | **PRESENT** | Validator report |
| `AirGestureAI-4.1.0-Portable.zip` | Distribution | **UNAVAILABLE** | Execution blocked by environment ACL limitation |
| `AirGestureAI-4.1.0-Setup.msi` | Installer | **UNAVAILABLE** | WiX toolset not installed in build environment |
| `Documentation.zip` | Bundle | **UNAVAILABLE** | Execution blocked by environment ACL limitation |
| `SDK.zip` | Bundle | **UNAVAILABLE** | Execution blocked by environment ACL limitation |

---

## 3. Documentation Inventory (`VERIFIED`)

- `Docs/DEPLOYMENT_GUIDE.md`
- `Docs/RELEASE_PROCESS.md`
- `Docs/MIGRATION_GUIDE.md`
- `Docs/INSTALLATION_GUIDE.md`
- `Docs/SECURITY_GUIDE.md`
- `Docs/RELEASE_NOTES.md`
- `Docs/LICENSE.md`
- `Docs/THIRD_PARTY_NOTICES.md`
- `README.md`
