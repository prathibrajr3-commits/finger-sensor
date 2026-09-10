# AirGesture AI v4.1 RTM — Security Guide

This document describes the complete security posture of AirGesture AI, including the vault architecture, IPC security pipeline, runtime protection strategy, and security audit reporting.

---

## Table of Contents

1. [Security Architecture Overview](#1-security-architecture-overview)
2. [SecureVault Architecture](#2-securevault-architecture)
3. [IPC Security Pipeline](#3-ipc-security-pipeline)
4. [Path & Input Validation](#4-path--input-validation)
5. [Plugin Validation](#5-plugin-validation)
6. [Binary Integrity Verification](#6-binary-integrity-verification)
7. [Runtime Protection](#7-runtime-protection)
8. [Security Audit Service](#8-security-audit-service)
9. [Security Report Schema](#9-security-report-schema)
10. [Threat Model](#10-threat-model)

---

## 1. Security Architecture Overview

AirGesture AI operates as a multi-process desktop application. Security is enforced at every layer:

```
┌──────────────────────────────────────────────────────────────────────┐
│                        Security Layers                               │
├──────────────────────────────────────────────────────────────────────┤
│  [UI Layer]      • Input validation on all user-supplied fields       │
│  [IPC Layer]     • Token auth + payload injection check              │
│  [Vault Layer]   • DPAPI master key + AES-256-GCM per-entry encrypt  │
│  [File Layer]    • Path traversal prevention, root-boundary enforce  │
│  [Plugin Layer]  • Manifest schema validation + duplicate detection  │
│  [Binary Layer]  • SHA-256 hash verification + Authenticode check    │
│  [Runtime Layer] • Continuous binary tamper + vault corruption checks│
│  [Audit Layer]   • Structured security_report.json on every run      │
└──────────────────────────────────────────────────────────────────────┘
```

---

## 2. SecureVault Architecture

### Encryption Design

The `SecureVault` implements a two-tier key hierarchy:

```
  Windows DPAPI (CurrentUser scope)
         │
         ▼ Protects
  ┌──────────────────┐
  │   Master Key     │  — 256-bit AES random key, stored as .master.key
  │  (DPAPI-blob)    │
  └───────┬──────────┘
          │ Used as AES-256-GCM key
          ▼
  ┌──────────────────────────────────────┐
  │ Per-entry AES-256-GCM encrypted blob │
  │  [12B nonce][16B auth-tag][ciphertext]│
  └──────────────────────────────────────┘
         │
         ▼ Stored as
  %LocalAppData%\AirGestureAI\SecureVault\<sanitized-key>.vault
```

### Key Features

| Feature | Implementation |
|---|---|
| Master Key Protection | Windows DPAPI `DataProtectionScope.CurrentUser` |
| Per-entry Encryption | AES-256-GCM with a fresh random 96-bit nonce per write |
| Key Rotation | `RotateKey()` — decrypts all entries, generates new 256-bit key, re-encrypts |
| Backup | `BackupAsync()` — copies all `.vault` files + `.master.key` to a `_backup` sibling directory |
| Automatic Recovery | `Retrieve()` falls back to backup if the primary vault file is corrupt |
| Integrity Check | `VerifyIntegrity()` — validates all `.vault` files are valid Base64 AES-GCM blobs |

### Vault File Layout

```
%LocalAppData%\AirGestureAI\
  SecureVault\
    .master.key          ← DPAPI-protected 256-bit AES master key
    api-key.vault        ← Base64(nonce + tag + ciphertext)
    token.vault
  SecureVault_backup\
    .master.key          ← Backup of master key
    api-key.vault        ← Backup copies
```

---

## 3. IPC Security Pipeline

Every Named Pipe message passes through the security middleware before reaching any handler.

```
Incoming IPC Request
        │
        ▼
┌────────────────────────────────────────────┐
│  SecurityMiddleware (IpcRouter)            │
│                                            │
│  ① ValidateToken(msg.Token)               │
│     • Must equal SECURE_AIRGESTURE_TOKEN_v4│
│     • Failure: reject + RecordAuthFailure  │
│                                            │
│  ② InputSecurity.Validate(msg.Payload)    │
│     • Scans for ; & | > < $ ` ' " \       │
│     • Failure: reject + warn log          │
│                                            │
│  ③ Execute handler(msg, ct)               │
│                                            │
│  ④ Audit log: Method + CorrelationId      │
└────────────────────────────────────────────┘
        │
        ▼
Dispatch to registered async handler
```

### Token Rotation Policy

The IPC token should be rotated on each release. The token is defined in `IpcSecurity.ValidateToken()`. In production, load it from `SecureVault` rather than hardcoding.

> [!IMPORTANT]
> Always call `router.UseSecurityMiddleware()` immediately after constructing an `IpcRouter`. This ensures no request can bypass the token check.

### Auth Failure Tracking

Failed authentications increment a counter in `RuntimeProtectionService`. After 5 consecutive failures, a `WARNING`-severity security alert is fired through `LoggingService`.

---

## 4. Path & Input Validation

### Path Traversal Prevention (`PathSecurity`)

```csharp
bool safe = PathSecurity.IsPathSafe(userProvidedPath, rootDirectory);
```

The check:
1. Normalizes both paths using `Path.GetFullPath()`.
2. Verifies the normalized target starts with the normalized root.
3. Rejects alternate data stream identifiers (second `:` in path).
4. All failures are logged via `Logger.Warn`.

### Command Injection Prevention (`InputSecurity`)

```csharp
var result = InputSecurity.Validate(payload, allowedTokens: null);
if (!result.IsValid)
    return $"Rejected: {result.ErrorMessage}";
```

**Default blacklist**: `;  &  |  >  <  $  \`  '  "  \`

Allowlist overrides are supported via the `allowedTokens` parameter.

---

## 5. Plugin Validation

All `plugin.json` manifests are validated by `PluginManifestValidator.Validate()`.

**Rules**:

| Field | Rule |
|---|---|
| `name` | Required. Must match `^[a-zA-Z0-9_\-]{2,64}$` |
| `version` | Required. Must follow semver `major.minor.patch` |
| `author` | Required. Non-empty string |
| `entryAssembly` | Required. Must end in `.dll` |
| `dependencies[]` | Each must match the name regex |
| Duplicate names | Detected across all loaded plugins and rejected |

The `SecurityAuditService` runs this check during each full audit sweep of the `Plugins/` directory.

---

## 6. Binary Integrity Verification

### SHA-256 Sidecar Files

At release packaging time, generate an integrity sidecar for each critical binary:

```csharp
BinarySigner.GenerateIntegrityFile("AirGestureAI.exe", "integrity.sha256");
```

At runtime, verify:

```csharp
var result = BinarySigner.VerifySha256("AirGestureAI.exe", "integrity.sha256");
if (!result.IsValid)
    TriggerSecurityAlert(result.ErrorMessage);
```

### Authenticode Signature

```csharp
var result = BinarySigner.ReadAuthenticodeSignature("AirGestureAI.exe");
// result.HasAuthenticodeSignature → true if signed
// result.SignerSubject → certificate subject DN
```

---

## 7. Runtime Protection

`RuntimeProtectionService` runs a background monitor every **30 seconds** checking:

| Check | Action on Failure |
|---|---|
| Binary file hash change | `WARNING` logged via `LoggingService` |
| Vault file corruption | `WARNING` + `_vaultCorruptionReported` flag set |
| Configuration JSON validity | `WARNING` logged |
| IPC auth failure count | Alert after ≥5 failures |
| Plugin load failure count | Alert after ≥3 failures |

Wire into the DI container and connect callbacks:

```csharp
var rts = serviceProvider.GetRequiredService<RuntimeProtectionService>();
var router = serviceProvider.GetRequiredService<IpcRouter>();
router.OnAuthFailure = rts.RecordIpcAuthFailure;
router.OnAuthSuccess = rts.RecordIpcAuthSuccess;
```

---

## 8. Security Audit Service

`SecurityAuditService.RunAuditAsync()` executes all 8 checks and produces a `security_report.json` in:

```
%LocalAppData%\AirGestureAI\Diagnostics\security_report.json
```

### Checks Executed

| # | Check | Severity on Fail |
|---|---|---|
| 1 | Binary Integrity (SHA-256 vs sidecar) | Critical |
| 2 | Unsigned Plugin Detection | Warning |
| 3 | Configuration Validation (JSON parse) | Critical |
| 4 | Vault Integrity (Base64 blob check) | Critical |
| 5 | IPC Token Hygiene (length + complexity) | Critical |
| 6 | File Permission Validation | Warning |
| 7 | Policy Validation (NetworkAccess blocked) | Critical |
| 8 | Plugin Manifest Validation | Warning |

### Composite Grade Calculation

| Condition | Grade |
|---|---|
| ≥3 critical failures | F |
| ≥1 critical failure | D |
| ≥4 warnings | C |
| ≥2 warnings | B |
| Clean | A |

---

## 9. Security Report Schema

```json
{
  "auditTimestamp": "2026-08-06T05:30:00.0000000Z",
  "grade": "A",
  "criticalCount": 0,
  "warningCount": 1,
  "checks": [
    "BinaryIntegrity",
    "UnsignedPluginDetection",
    "ConfigurationValidation",
    "VaultIntegrity",
    "IpcTokenValidation",
    "FilePermissionValidation",
    "PolicyValidation",
    "PluginManifestValidation"
  ],
  "findings": [
    {
      "check": "BinaryIntegrity",
      "severity": "Info",
      "description": "Binary integrity verified successfully.",
      "remediation": "",
      "passed": true
    },
    {
      "check": "UnsignedPluginDetection",
      "severity": "Warning",
      "description": "Unsigned plugin assemblies detected: SomePlugin.dll.",
      "remediation": "Sign all plugin assemblies with an Authenticode certificate before distribution.",
      "passed": false
    }
  ]
}
```

---

## 10. Threat Model

| Threat | Mitigation |
|---|---|
| Path traversal via user input | `PathSecurity.IsPathSafe()` with root boundary enforcement |
| Command injection in IPC payload | `InputSecurity.Validate()` blacklisting `;`, `&`, `|`, `>`, `<`, `$`, `` ` `` |
| IPC token brute-force | `RuntimeProtectionService` alerts after 5 failures |
| Credential theft from disk | AES-256-GCM per-entry encryption; DPAPI-protected master key |
| Vault corruption | `VerifyIntegrity()` + automatic backup recovery |
| Binary tampering | SHA-256 sidecar + Authenticode + RuntimeProtection monitor |
| Rogue plugin loading | Manifest schema validation + duplicate detection |
| Unauthorized subprocess launch | `PolicyEngine` restricts launches to AirGestureAI processes only |
| External network access | `PolicyEngine.NetworkAccess` blocks all non-localhost destinations |

> [!WARNING]
> The IPC shared secret token (`SECURE_AIRGESTURE_TOKEN_v4`) is a hardcoded default. In production deployments, load it from `SecureVault` and rotate it on each major release.

> [!CAUTION]
> The AES master key is DPAPI-protected under `DataProtectionScope.CurrentUser`. Moving the vault files to a different user account or machine will make all stored secrets unrecoverable unless a backup was exported beforehand.
