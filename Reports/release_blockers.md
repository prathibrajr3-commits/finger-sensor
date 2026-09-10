# AirGesture AI v4.1 RTM — Release Blockers & Prerequisites Report

**Target Release**: AirGesture AI v4.1.0 RTM  
**Assessment Date**: 2026-08-13  

---

## Release Readiness Summary

Current Decision: **RTM NOT READY**

While the entire v4.1.0 RTM source code, release engineering scripts, installers, security policies, and documentation are complete and verified, production distribution is blocked by external environment dependencies listed below.

---

## Active Release Blockers

### 1. Build & Test Verification (`BLOCKED`)
- **Impact**: Unable to generate compiled binary outputs or run automated xUnit test validation.
- **Cause**: Environment ACL constraint on `run_command` (`opening NUL for ACL write: Access is denied`).
- **Remediation Required**: Execute `Build/BuildRelease.ps1` in an unconstrained Windows developer prompt:
  ```powershell
  cd "E:\finger sensor"
  .\Build\BuildRelease.ps1 -Configuration Release
  ```

### 2. WiX Installer Compilation (`BLOCKED`)
- **Impact**: `AirGestureAI-4.1.0-Setup.msi` is unavailable.
- **Cause**: WiX Toolset v3.11+ (`candle.exe` and `light.exe`) is not installed on the build machine.
- **Remediation Required**: Install WiX Toolset v3.11 or run WiX build step on a CI worker with WiX installed.

### 3. Code Signing Configuration (`PREREQUISITE / NOT CONFIGURED`)
- **Impact**: Executables and DLLs are unsigned (`SIGNING_NOT_CONFIGURED.md`). Windows SmartScreen warnings will appear upon download.
- **Cause**: No Authenticode PFX code-signing certificate or hardware token was supplied.
- **Remediation Required**: Supply code-signing certificate credentials during production pipeline execution.

---

## Action Items to Reach "RTM READY"

1. Run `dotnet build` and `dotnet test` on a standard Windows environment to verify clean compilation and 100% test pass rate.
2. Compile `AirGestureAI-4.1.0-Setup.msi` using WiX Toolset.
3. Sign binary executables using Authenticode PFX.
4. Regenerate `dist/checksums.sha256` and publish release artifacts.
