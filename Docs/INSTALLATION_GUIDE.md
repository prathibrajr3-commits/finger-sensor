# AirGesture AI v4.1.0 — Installation Guide

---

## System Requirements

- **Operating System**: Windows 10 / Windows 11 (64-bit)
- **Framework**: .NET 8.0 Runtime (Desktop)
- **Camera**: Standard USB Webcam or Integrated Camera (720p 30fps recommended)
- **Hardware Acceleration**: DirectX 12 compatible GPU (DirectML) or NVIDIA CUDA GPU (optional for ONNX acceleration)

---

## Installation Options

### Option 1: WiX Windows Installer (.msi)
1. Download `AirGestureAI-4.1.0-Setup.msi`.
2. Run the installer and complete the setup wizard.
3. AirGesture AI will be installed to `C:\Program Files\AirGestureAI`.
4. Shortcuts will be created in the Start Menu and Desktop (if selected).

### Option 2: Portable Package (.zip)
1. Download `AirGestureAI-4.1.0-Portable.zip`.
2. Extract the archive contents to any folder (e.g. `D:\AirGestureAI`).
3. Launch `AirGestureAI.exe` directly.

---

## Silent Enterprise Deployment

Administrators can deploy AirGesture AI across enterprise networks using `msiexec`:

```cmd
msiexec /i AirGestureAI-4.1.0-Setup.msi /qn /norestart
```

To remove the application silently:

```cmd
msiexec /x AirGestureAI-4.1.0-Setup.msi /qn /norestart
```
