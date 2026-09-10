# AirGesture AI v4.1.0 RTM — Final Release Engineer Handoff

**Product**: AirGesture AI  
**Version**: 4.1.0 RTM  
**Target Architecture**: win-x64 (.NET 8.0-windows)  
**Handoff Date**: 2026-08-24  
**Current Decision**: `RTM NOT READY` (Pending real Windows execution evidence)

---

## 1. Objective

AirGesture AI v4.1.0 source implementation is **complete through RTM Phase 10**.  
To transition from `RTM NOT READY` to `RTM READY`, the Release Engineer must execute the release pipeline commands on a real Windows development machine and submit the **exact, raw terminal output** without truncation or modification.

---

## 2. Mandatory Verification Commands

Open a Windows PowerShell prompt (with appropriate execution permissions) and run the following exact commands in sequence from the project root:

```powershell
# Step 1: Navigate to project root
cd "E:\finger sensor"

# Step 2: Tooling & Runtime Environment Check
dotnet --version

# Step 3: Clean Workspace
dotnet clean

# Step 4: Restore Dependencies
dotnet restore

# Step 5: Release Build with Zero Warnings Allowed
dotnet build "AirGestureAI.csproj" --configuration Release /p:TreatWarningsAsErrors=true

# Step 6: Execute Full Test Suite
dotnet test "Tests\AirGestureAI.Tests.csproj" --configuration Release

# Step 7: Build Release Packages & Execute Release Validator
.\Build\BuildRelease.ps1 -Configuration Release

# Step 8: Execute Settings Migration Verification (Optional/Secondary)
powershell -ExecutionPolicy Bypass -File .\Installer\migrate-settings.ps1 -SourceVersion "4.0.0" -TargetVersion "4.1.0"
```

---

## 3. Required Evidence Guidelines

The certification authority requires **RAW terminal output** for every executed step.

### Strictly Unacceptable:
- Summaries or paraphrased descriptions
- Screenshots without raw terminal text
- "It passed" or "Build succeeded" assertions
- Static code inspection claims
- Placeholder text (e.g., `[PASTE RESULT]`)

---

## 4. Gate Classification Criteria

Upon receipt of the real raw execution output, gates will be classified as:

- **Release Build**: `VERIFIED` (0 errors, 0 warnings) / `FAILED`
- **Full Test Suite**: `VERIFIED` (100% pass) / `FAILED`
- **Portable Package**: `VERIFIED` (ZIP physically exists and validated) / `FAILED`
- **Checksums**: `VERIFIED` (SHA-256 matches generated files) / `FAILED`
- **Settings Migration**: `VERIFIED` (v4.0.0 -> v4.1.0 migration passes) / `FAILED`
- **Security Validation**: `VERIFIED` (0 secrets, PDBs, or unisolated tokens) / `FAILED`
- **Release Validator**: `VERIFIED` (JSON report valid and pass) / `FAILED`
- **MSI Package**: `VERIFIED` / `NOT AVAILABLE` / `FAILED`
- **Authenticode Signing**: `VERIFIED` / `NOT CONFIGURED` / `FAILED`

---

## 5. Expected Physical Artifacts in `dist\`

The pipeline is expected to physically create and validate the following artifacts:
1. `dist\AirGestureAI-4.1.0-Portable.zip`
2. `dist\Documentation.zip`
3. `dist\SDK.zip`
4. `dist\AirGestureAI-4.1.0-Setup.msi` *(Optional — requires WiX Toolset)*
5. `dist\checksums.sha256`
6. `dist\release_manifest.json`
7. `dist\release_validation_report.json`
8. `dist\SBOM.spdx.json`

---

## 6. Certification Rule

Only when all mandatory gates (Build, Tests, Portable Package, Checksums, Migration, Security, Release Validator) have actual execution evidence and pass will the final status transition to:

```
RTM READY
```

Otherwise, the status remains:

```
RTM NOT READY
```
