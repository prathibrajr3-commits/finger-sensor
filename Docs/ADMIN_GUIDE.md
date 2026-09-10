# AirGesture AI v4.0.0 — Administrator Guide

This guide is intended for system administrators managing enterprise deployments, security policies, and performance tuning of the AirGesture AI v4.0.0 desktop platform.

---

## 1. Subprocess Architecture & CLI Arguments

AirGesture AI runs in a multi-process isolated architecture. The primary WPF GUI executable coordinates the lifecycle of two subprocesses:

1. **Tracker Host (`--tracker-host`)**: Starts the MediaPipe Python hand-tracking named-pipe IPC server.
2. **AI Worker (`--ai-worker`)**: Starts the C# ONNX Runtime model inference named-pipe IPC server.

Admin commands can inspect active subprocesses:
- `tasklist | findstr /i AirGesture`

---

## 2. Enterprise Policy Configuration

The security engine evaluates policies in `SecuritySubsystem.cs` using the `PolicyEngine` class. Rules can be configured to enforce restrictions:

- **ProcessLaunch**: Restricts which executable binaries the application can launch.
- **PluginLoad** / **PluginInstall**: Controls if third-party extensions can load from untrusted directories.
- **NetworkAccess**: Hardcoded to local-loopback only to ensure complete data privacy (no telemetry or coordinates leave the device).
- **FileAccess**: Restricts file modifications outside the designated workspace directories.

---

## 3. Data Protection and Vault Storage

Sensitive settings (such as custom token keys, configuration overrides, or certificates) are saved locally under:
- `AppData/Local/AirGestureAI/SecureVault`

All credential keys are encrypted using Windows DPAPI (`DataProtectionScope.CurrentUser`). Backups can be triggered via the UI to save AES-256 encrypted archives of the keys.

---

## 4. Subprocess Heartbeats and Recovery

The **ServiceHost** verifies subprocess lifecycles every 5 seconds. If a process stops responding:
1. It registers an alert in the audit log.
2. It attempts self-recovery by re-launching the executable with the corresponding CLI parameters.
3. If recovery fails multiple times, it degrades the component's health status in the UI dashboard.
