# AirGesture AI v4.1.0 RTM — Release Distribution & Deployment Guide

This guide describes how to acquire, verify, deploy, and operate the **AirGesture AI v4.1.0 RTM** release package.

---

## 1. Release Classification & Governance

| Gate / Criterion | Evaluation Status | Description / Condition |
|---|---|---|
| **Compilation Build** | `VERIFIED` | Clean Release compilation; 0 errors, 0 warnings (.NET 8.0 Windows x64) |
| **Test Suite** | `VERIFIED` | 104 of 104 tests passing (0 failed, 0 skipped) |
| **Portable Package** | `VERIFIED` | 38.89 MB archive, runtime-pruned for Windows x64 |
| **Integrity Checksums** | `VERIFIED` | 6 of 6 release artifacts match SHA-256 digests in `checksums.sha256` |
| **SBOM Specification** | `VERIFIED` | SPDX 2.3 JSON specification generated on disk |
| **Release Validator** | `VERIFIED` | 6 of 6 validator checks passed (`OverallStatus = true`) |
| **Settings Migration** | `VERIFIED` | v4.0.0 → v4.1.0 migration verified in sandbox; all user assets preserved, 0 secrets exported |
| **Package Secret Audit** | `VERIFIED` | 0 credentials, 0 API keys, 0 private keys, 0 personal paths, 0 debug symbols |
| **Deep CVE Scan** | `DEEP CVE SCAN = NOT AVAILABLE` | Offline build sandbox lacked access to live external vulnerability registries |
| **MSI Installer** | `NOT AVAILABLE` | WiX Toolset not installed in build environment |
| **Authenticode Signing**| `NOT CONFIGURED` | Code signing certificate not provisioned on build host |
| **Final Release Status**| **`RTM READY WITH EXCEPTIONS`** | Certified for general distribution as a portable Windows x64 package |

---

## 2. Release Artifacts

All distribution artifacts reside in the `dist/` directory:

| Filename | File Size | SHA-256 Checksum | Purpose |
|---|---|---|---|
| `AirGestureAI-4.1.0-Portable.zip` | 40,782,656 B | `8c5472b8d6eb5cd05417c7bc2b76d60079287cfa173a823ed9aaeff3ec315b35` | Main application runtime bundle |
| `Documentation.zip` | 43,456 B | `47b7c03f3abeef86f03db88832df6fff0d39b3974a646f062dc094d5166403e3` | Complete release documentation set |
| `SDK.zip` | 9,818 B | `15a10c198de0ffd93581b17f9982d018d04d960aa39a107fb0d87e9a4042f306` | Plugin interfaces and development contracts |
| `SBOM.spdx.json` | 2,974 B | `5772a4aaee4a30160171ffde4d535f47e3589a54e2d3260a41ed13223d5a9958` | SPDX 2.3 Software Bill of Materials |
| `release_manifest.json` | 1,555 B | `87a319993bed9d43635654b7b364d2b3950d953bc9b6ce179a6834970827900c` | Package manifest with build metadata |
| `release_validation_report.json` | 1,785 B | `577704f356988a91fc85c071c8285753ba1d5d08aeb720dd0ffd5bb6c0f914e6` | Release gate validator scorecard |
| `checksums.sha256` | 531 B | *(Manifest index)* | SHA-256 cryptographic hash index |

---

## 3. Verification & Deployment Instructions

### Step 1: Verify Package Integrity
Before unpacking the distribution, compute its cryptographic hash and match it with `checksums.sha256`:

```powershell
$hash = (Get-FileHash -Path .\dist\AirGestureAI-4.1.0-Portable.zip -Algorithm SHA256).Hash.ToLowerInvariant()
if ($hash -eq "8c5472b8d6eb5cd05417c7bc2b76d60079287cfa173a823ed9aaeff3ec315b35") {
    Write-Host "Verification succeeded: Checksum matches release record." -ForegroundColor Green
} else {
    Write-Error "Verification failed: Checksum mismatch!"
}
```

### Step 2: Deployment
1. Extract the contents of `AirGestureAI-4.1.0-Portable.zip` to your target directory (e.g., `C:\Program Files\AirGestureAI` or `%LOCALAPPDATA%\AirGestureAI\app`).
2. Verify that the `.NET 8.0 Desktop Runtime (x64)` is installed on the host system.

### Step 3: Execution
1. Launch `AirGestureAI.exe`.
2. As Authenticode code signing is **NOT CONFIGURED**, Windows SmartScreen will display an informational banner. Click **More info** → **Run anyway**.
3. Grant camera access when prompted by Windows privacy settings.

---

## 4. Disclosures & Known Limitations

- **No Windows Installer**: MSI packaging is **NOT AVAILABLE**. IT administrators should deploy the portable bundle directly or repackage with internal enterprise tooling.
- **Unsigned Binaries**: Code signing is **NOT CONFIGURED**. Binaries must be evaluated according to your internal security policies before enterprise rollout.
- **No Deep CVE Scan**: While package secret hygiene was fully verified (no credentials, no private keys, no developer paths, and no debug symbols), **DEEP CVE SCAN = NOT AVAILABLE** due to the offline build sandbox. We do not claim the package is fully secure from file analysis alone.
- **Architecture**: Strictly **Windows x64**. Other operating systems and CPU architectures are not supported.
