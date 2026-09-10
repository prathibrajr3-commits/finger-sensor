# AirGesture AI v4.1.0 RTM

AirGesture AI is a high-performance, offline-first desktop air gesture control platform designed for Windows x64. It translates touchless hand gestures captured via standard webcams into intuitive operating system actions and desktop automation. The application incorporates process-isolated coordinate tracking, hardware-accelerated local ONNX neural inference, a crash recovery restoration chain, and a secure local policy engine.

---

## Release Status & Governance Notice

> [!IMPORTANT]
> **Current Release Decision**: `RTM READY WITH EXCEPTIONS`  
> AirGesture AI v4.1.0 has completed all core software compilation, automated testing (104/104 passed), runtime packaging, SBOM generation, checksum validation, and settings migration verification. Please note the following disclosures and operational boundaries:
> 
> - **Supported Architecture**: Strictly **Windows x64** (`win-x64`). Unnecessary runtime assets for non-Windows and 32-bit platforms have been pruned.
> - **Distribution Vehicle**: **Portable ZIP archive** (`dist\AirGestureAI-4.1.0-Portable.zip`).
> - **MSI Installer**: **NOT AVAILABLE** — The Windows Installer (.msi) is not available in this release because the WiX Toolset (`candle.exe`/`light.exe`) is not provisioned in the build environment. The self-contained portable package is the primary distribution format.
> - **Code Signing**: **NOT CONFIGURED** — Production Authenticode code signing certificates / HSM are not configured on the build host. Binaries are unsigned. See [SIGNING_NOT_CONFIGURED.md](SIGNING_NOT_CONFIGURED.md) for enterprise mitigation instructions.
> - **Security & Vulnerability Scanning**: Package secret and artifact hygiene has been verified (zero embedded credentials, zero private keys, zero auth tokens, zero personal machine paths, and zero debug symbols). However, **DEEP CVE SCAN = NOT AVAILABLE** because external automated dependency CVE scanners were unavailable and live feeds were not reachable in the isolated build sandbox. Full security is **not** claimed from file-content scanning alone.

---

## Key Capabilities

- 🖥️ **Process Isolation**: Camera capture and coordinate tracking run in isolated background worker processes, ensuring the WPF UI thread never stutters or drops frames.
- 🔗 **Hardened IPC**: Inter-Process Communication utilizes local Named Pipe servers secured by token validations and structured JSON messaging.
- 🧠 **Hardware-Accelerated Neural Inference**: Local ONNX models execute via Microsoft ML.OnnxRuntime with auto-detection for **DirectML** GPU acceleration and automatic multi-threaded CPU fallback.
- 🛡️ **Session Crash Recovery**: Automatic startup lock detection, multi-tier state recovery chain (primary state, backup snapshot, and diagnostic autosaves), and automatic Safe Mode activation after consecutive faults.
- 🔒 **Local Security Vault**: Credential storage encrypted using Windows Data Protection API (DPAPI) and governed by a local localhost-only policy engine.
- 📦 **Settings & Profile Migration**: Built-in automated settings migration preserving gesture profiles, workflows, lens calibrations, and preferences while strictly isolating sensitive secrets.

---

## System Requirements

| Requirement | Specification |
|---|---|
| **Operating System** | Windows 10 (version 1809+) or Windows 11 (64-bit) |
| **Architecture** | **x64 (AMD64 / Intel 64)** only |
| **Runtime** | [.NET 8.0 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/8.0) |
| **Camera** | Any standard USB or integrated RGB webcam (720p @ 30 FPS or higher recommended) |
| **GPU / Acceleration** | DirectX 12 compatible GPU (recommended for DirectML acceleration); multi-core x64 CPU fallback supported |
| **Memory & Storage** | Minimum 4 GB RAM (8 GB recommended); ~150 MB free disk space |
| **Python (Source Build Only)** | Python 3.10+ (only required if developing from source; bundled runtime handled in packaged distribution) |

---

## Installation & First-Run Steps

### 1. Download & Verification
1. Download `AirGestureAI-4.1.0-Portable.zip` and `checksums.sha256` from the official release distribution.
2. In PowerShell, verify the SHA-256 integrity of the package before extraction:
   ```powershell
   Get-FileHash .\AirGestureAI-4.1.0-Portable.zip -Algorithm SHA256
   ```
   Confirm the output matches the entry in `checksums.sha256`:
   `8c5472b8d6eb5cd05417c7bc2b76d60079287cfa173a823ed9aaeff3ec315b35`

### 2. Extract Portable Package
1. Extract `AirGestureAI-4.1.0-Portable.zip` to a folder of your choice (e.g., `C:\AirGestureAI` or `%LOCALAPPDATA%\AirGestureAI\app`).
2. Ensure the destination directory has user read and write permissions.

### 3. Launching the Application
1. Double-click `AirGestureAI.exe` to launch the application.
2. **Windows SmartScreen Note**: Because Authenticode signing is **NOT CONFIGURED** for this build, Windows SmartScreen may prompt with *"Windows protected your PC"*. Click **More info** → **Run anyway**.
3. **Camera Permissions**: Ensure that Camera access is enabled in Windows Settings (*Settings > Privacy & Security > Camera*).
4. On startup, the operations dashboard will initialize:
   - Camera frame acquisition connects.
   - Background tracking host establishes Named Pipe IPC.
   - Neural inference engine selects DirectML or CPU fallback.
5. Position your hand approximately 0.5 to 1.5 meters from the camera lens to begin tracking.

---

## Upgrading from v4.0.x

To upgrade an existing installation and migrate user profiles:
1. Extract the new v4.1.0 binaries into your application folder.
2. Run the migration script to backup and migrate your settings:
   ```powershell
   powershell -ExecutionPolicy Bypass -File .\Installer\migrate-settings.ps1 -SourceVersion "4.0.0" -TargetVersion "4.1.0"
   ```
3. The migration pipeline:
   - Preserves gesture profiles (`gesture_profiles/`), workflows (`workflows/`), camera lens calibration (`calibration/`), plugin configs (`plugins/`), and user preferences (`AppConfig.json`).
   - Creates a timestamped safety backup in `%LocalAppData%\AirGestureAI\MigrationBackup_4.0.0`.
   - Strictly isolates credentials, passwords, auth tokens, and DPAPI key material from export.

---

## Building from Source

To build AirGesture AI locally from source code:

```powershell
# 1. Clone repository
git clone https://github.com/AirGestureAI/AirGestureAI.git
cd AirGestureAI

# 2. Restore and compile in Release configuration
dotnet restore
dotnet build AirGestureAI.csproj -c Release --no-restore

# 3. Run complete test suite (104 tests)
dotnet test Tests\AirGestureAI.Tests.csproj -c Release

# 4. Package portable win-x64 distribution
.\Build\BuildRelease.ps1 -Configuration Release -RuntimeIdentifier win-x64 -SkipInstaller
```

---

## Known Limitations

1. **Architecture Limitation**: Built and pruned specifically for Windows x64 (`win-x64`). 32-bit Windows, ARM64 Windows, Linux, and macOS platforms are not supported in this build.
2. **Installer Format**: Windows Installer (.msi) packages are **NOT AVAILABLE**; deployment must be managed via the portable distribution.
3. **Code Signing**: Binaries are unsigned (**NOT CONFIGURED**). Enterprises deploying via group policy or endpoint management tools should sign the binary bundle with their internal code signing certificate.
4. **Offline Vulnerability Feeds**: Deep live CVE dependency scanning was **NOT AVAILABLE** during offline sandbox packaging. Automated package cleanliness and secret hygiene checks passed completely (zero secrets, zero debug artifacts).
5. **Lighting & Occlusion**: Optical tracking accuracy depends on standard lighting conditions. Extreme backlighting or heavily obscured camera views will degrade tracking precision.

---

## License & Third-Party Notices

AirGesture AI is licensed under the Apache License, Version 2.0.  
See [Docs/LICENSE.md](Docs/LICENSE.md) and [Docs/THIRD_PARTY_NOTICES.md](Docs/THIRD_PARTY_NOTICES.md) for license grants, copyright notices, and third-party attributions.
