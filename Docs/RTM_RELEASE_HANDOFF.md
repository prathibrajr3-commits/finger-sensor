# AirGesture AI v4.1.0 — RTM Release Handoff & Developer Guide

This document provides step-by-step instructions for release engineers and Windows developers to execute final build compilation, test verification, installer generation, code signing, and release publication for **AirGesture AI v4.1.0 RTM**.

---

## 1. Environment Prerequisites

Before executing the release pipeline, verify the following tools are installed on your Windows build environment:

| Dependency | Minimum Required Version | Verification Command | Purpose |
|---|---|---|---|
| **.NET SDK** | .NET 8.0 SDK (8.0.x) | `dotnet --version` | Application compilation & testing |
| **PowerShell** | 5.1+ / 7.x | `powershell $PSVersionTable` | Master release pipeline execution |
| **WiX Toolset** | v3.11+ | `where.exe candle.exe`, `where.exe light.exe` | Windows Installer (.msi) compilation |
| **Windows SDK** | 10.0+ (SignTool) | `where.exe signtool.exe` | Authenticode code signing |
| **Git** | 2.x | `git --version` | Source control & tag management |
| **Python** | 3.8 – 3.11 (with MediaPipe) | `python --version` | Hand tracking subprocess models |

---

## 2. Clean Build Compilation

Open a PowerShell terminal as Administrator, navigate to the repository root, and run:

```powershell
cd "E:\finger sensor"

# Clean build artifacts
dotnet clean

# Restore NuGet dependencies
dotnet restore "AirGestureAI.csproj"

# Release build with TreatWarningsAsErrors enabled
dotnet build "AirGestureAI.csproj" --configuration Release /p:TreatWarningsAsErrors=true
```

**Verification Gate**: Must return exit code `0` with **0 errors** and **0 warnings**.

---

## 3. Comprehensive Test Suite Execution

Run all xUnit test suites across core runtime, diagnostics, recovery, security, and packaging:

```powershell
dotnet test "Tests\AirGestureAI.Tests.csproj" --configuration Release
```

**Verification Gate**: Must report **100% passed** across all test classes:
- `ArchitectureTests`
- `ResearchTests`
- `Sprint1Tests`
- `AsyncIpcTests`
- `SecurityHardeningTests`
- `DiagnosticsTests`
- `CrashRecoveryTests`
- `CrashRecoveryIntegrationTests`
- `ReleasePackagingTests`

---

## 4. Master Release Pipeline Execution

Run the master packaging script to publish binaries, assemble the portable archive, package documentation/SDK bundles, and generate release metadata:

```powershell
.\Build\BuildRelease.ps1 -Configuration Release
```

The script automatically executes:
1. `dotnet publish` targeting `win-x64`
2. `BuildPortable.ps1` to assemble `dist/AirGestureAI-4.1.0-Portable.zip`
3. Archives `dist/Documentation.zip` and `dist/SDK.zip`
4. Generates `dist/release_manifest.json` and `dist/SBOM.spdx.json`
5. Computes SHA-256 digests in `dist/checksums.sha256`
6. Runs `ReleaseValidator` producing `dist/release_validation_report.json`

---

## 5. WiX Installer (.msi) Compilation

If WiX Toolset v3.11+ is installed on the build machine, compile `AirGestureAI-4.1.0-Setup.msi`:

```powershell
cd "E:\finger sensor\Installer"

# Compile WiX source into object file
candle.exe AirGestureAI.wxs -out AirGestureAI.wixobj -dSourceDir="..\bin\Release\net8.0-windows\win-x64\publish"

# Link object file into MSI installer
light.exe AirGestureAI.wixobj -out "..\dist\AirGestureAI-4.1.0-Setup.msi" -ext WixUIExtension
```

Verify `dist/AirGestureAI-4.1.0-Setup.msi` is created and update `dist/checksums.sha256`.

---

## 6. Authenticode Code Signing

To sign release binaries for enterprise trust and bypass Windows SmartScreen warnings:

### Certificate Preparation
Ensure a valid Windows Authenticode Code Signing PFX certificate or HSM token is available.

### Binary Signing Command
```cmd
signtool sign /f "path\to\code_signing.pfx" /p "YourCertPassword" /tr http://timestamp.digicert.com /td sha256 /fd sha256 dist\AirGestureAI-4.1.0-Portable.zip dist\AirGestureAI-4.1.0-Setup.msi
```

### Signature Inspection
Inspect signature validity using `SDK.BinarySigner`:
```csharp
var result = AirGestureAI.SDK.BinarySigner.ReadAuthenticodeSignature(@"dist\AirGestureAI.exe");
Console.WriteLine($"Signer: {result.SignerSubject}, State: {result.SignatureState}");
```

---

## 7. Final Checksum Regeneration

After code signing and MSI generation, regenerate checksums:

```powershell
.\Installer\generate-checksums.ps1 -DistDir "dist"
```

---

## 8. Git Release Tagging

After all artifacts are verified and signed, create the release tag in the repository:

```powershell
# Ensure working tree is clean
git status

# Create annotated release tag
git tag -a v4.1.0 -m "AirGesture AI v4.1.0 RTM — Production Release"

# Push tag to origin
git push origin v4.1.0
```

**Required prior to tagging:**
- All mandatory release gates must be `VERIFIED` in `Reports/FINAL_RTM_CERTIFICATION.md`.
- `dist/AirGestureAI-4.1.0-Portable.zip` must physically exist and be checksum-verified.
- Authenticode signatures must be applied or waived with documented approval.

---

## 9. GitHub / Distribution Release Publication

After tagging, publish a GitHub Release:

1. Navigate to **GitHub → Releases → Draft a new release**.
2. Select tag `v4.1.0`.
3. Title: `AirGesture AI v4.1.0 RTM`.
4. Upload the following artifacts:
   - `dist/AirGestureAI-4.1.0-Portable.zip`
   - `dist/AirGestureAI-4.1.0-Setup.msi` *(if WiX compiled)*
   - `dist/Documentation.zip`
   - `dist/SDK.zip`
   - `dist/checksums.sha256`
   - `dist/SBOM.spdx.json`
5. Paste release notes from `Docs/RELEASE_NOTES.md`.
6. Set pre-release to **OFF** for RTM.
7. Publish release.

---

## 10. Known RTM Blockers (As of 2026-08-13)

The following items require resolution on a clean Windows developer build machine before RTM can be declared. **None of these block source completeness** — all Phase 1–8 implementation is done.

| Blocker | Status | Required Action |
|---|---|---|
| **Release Build Compilation** | `NOT VERIFIED` | Run `dotnet build` in unconstrained PowerShell |
| **Full Test Suite** | `NOT VERIFIED` | Run `dotnet test` in unconstrained PowerShell |
| **Portable ZIP Generation** | `NOT VERIFIED` | Run `Build/BuildRelease.ps1` |
| **WiX MSI Installer** | `NOT AVAILABLE` | Install WiX Toolset v3.11+ then compile `Installer/AirGestureAI.wxs` |
| **Authenticode Signing** | `NOT CONFIGURED` | Supply PFX certificate and timestamp URL to SignTool |

See `Reports/RTM_RELEASE_BLOCKERS.md` for full remediation details.

---

## 11. Final Release Handoff Checklist

Use this checklist to gate the final `RTM READY` decision:

```
ENVIRONMENT VERIFICATION
  [ ] dotnet --version reports 8.0.x
  [ ] PowerShell 5.1+ or 7.x confirmed
  [ ] WiX candle.exe + light.exe on PATH
  [ ] signtool.exe on PATH
  [ ] Python 3.8–3.11 with MediaPipe installed

BUILD & TEST
  [ ] dotnet clean completed
  [ ] dotnet restore completed (0 errors)
  [ ] dotnet build --configuration Release exit code = 0, 0 warnings, 0 errors
  [ ] dotnet test exit code = 0, 100% tests passed

PACKAGING
  [ ] Build\BuildRelease.ps1 executed to completion
  [ ] dist\AirGestureAI-4.1.0-Portable.zip exists and is non-empty
  [ ] dist\Documentation.zip exists
  [ ] dist\SDK.zip exists
  [ ] dist\release_manifest.json exists and valid
  [ ] dist\SBOM.spdx.json exists and valid
  [ ] dist\checksums.sha256 verified
  [ ] dist\release_validation_report.json shows PASS

INSTALLER (optional path if WiX available)
  [ ] candle.exe AirGestureAI.wxs succeeded
  [ ] light.exe produced dist\AirGestureAI-4.1.0-Setup.msi
  [ ] MSI installs/uninstalls cleanly in test VM
  [ ] MSI checksum appended to dist\checksums.sha256

CODE SIGNING
  [ ] All binaries signed with Authenticode SHA-256
  [ ] Timestamp applied via http://timestamp.digicert.com
  [ ] SDK.BinarySigner.ReadAuthenticodeSignature() returns Valid
  [ ] Checksums regenerated after signing

GIT & RELEASE
  [ ] git tag -a v4.1.0 -m "..." applied
  [ ] git push origin v4.1.0 pushed to remote
  [ ] GitHub Release created with all artifacts uploaded
  [ ] Reports/FINAL_RTM_CERTIFICATION.md updated to RTM READY

FINAL SIGN-OFF
  Release Engineer: ___________________________  Date: __________
  QA Lead:          ___________________________  Date: __________
  Product Owner:    ___________________________  Date: __________
```

---

*Document prepared as part of AirGesture AI v4.1.0 RTM Phase 10 — Release Handoff Engineering.*  
*All Phase 1–8 implementation is source-complete. Runtime verification must be performed on an unconstrained Windows developer build environment.*
