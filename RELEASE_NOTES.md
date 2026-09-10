# AirGesture AI v4.1.0 RTM — Release Notes

**Product**: AirGesture AI  
**Release Version**: 4.1.0 RTM  
**Target Architecture**: Windows x64 (`win-x64`)  
**Release Date**: 2026-09-10  
**Release Status**: `RTM READY WITH EXCEPTIONS`  

---

## Overview

AirGesture AI v4.1.0 RTM is a production-ready, offline-first touchless desktop gesture control system for Windows x64. This release delivers significant runtime hardening, automated multi-tier session crash recovery, process-isolated coordinate tracking, hardware-accelerated local ONNX inference, an automated v4.0.x → v4.1.0 profile migration engine, and comprehensive software bill of materials (SBOM) auditing.

---

## Key Highlights & Changes in v4.1.0

### 1. Crash Recovery & Resilience Subsystem
- **Startup Crash Detection**: Detects ungraceful shutdowns using timestamped active session lock files.
- **Three-Tier Restoration Chain**: Automatically restores session state sequentially from primary JSON, backup snapshots, or diagnostic autosaves.
- **Safe Mode Protection**: Activates Safe Mode with minimal plugin configurations after 3 consecutive startup failures to break crash loops.
- **WPF Recovery Banner**: Real-time visual status bar indicating recovery source and schema validation health.

### 2. Packaging & Release Engineering
- **Architecture-Optimized Portable Package**: Compiled for `.NET 8.0-windows` targeting `win-x64`. Pruned unnecessary non-Windows and 32-bit native runtime packages to produce a lean 38.89 MB self-contained bundle.
- **Full Test Suite Validation**: 104 of 104 automated tests passing (0 failures, 0 skipped) across all core subsystems.
- **SPDX 2.3 SBOM**: Machine-readable Software Bill of Materials in SPDX 2.3 JSON format (`dist/SBOM.spdx.json`).
- **Release Verification & SHA-256 Checksums**: Automated release validator execution (6/6 checks passed) and cryptographically verified SHA-256 hash manifest (`dist/checksums.sha256`).

### 3. Settings & Profile Migration (v4.0.x → v4.1.0)
- **Automated Schema Ingestion**: Verified migration script (`Installer/migrate-settings.ps1`) preserving gesture profiles, desktop automation workflows, lens calibration matrices, plugin configurations, and user preferences into timestamped backups (`MigrationBackup_4.0.0`).
- **Strict Credential Isolation**: Automatically isolates and prevents migration or export of master keys, DPAPI data, AES vault secrets, and authorization tokens.

### 4. Package Hygiene & Security Hardening
- **Prohibited Artifact Elimination**: Package audit confirmed 0 source files (`.cs`, `.csproj`), 0 debug symbols (`.pdb`), 0 temporary build items (`obj/`, `bin/`), and 0 personal machine paths.
- **Secret & Key Material Inspection**: Static inspection across all 44 package items confirmed zero embedded credentials, zero API tokens, and zero unencrypted keys.

---

## Release Disclosures & Operating Boundaries

In accordance with strict release governance, the following disclosures apply:

1. **RTM Status**: The release is certified as **RTM READY WITH EXCEPTIONS**. Core compilation, testing, packaging, validation, and migration gates are fully verified.
2. **Distribution Vehicle**: Distributed exclusively as a **Windows x64 Portable ZIP package** (`dist\AirGestureAI-4.1.0-Portable.zip`).
3. **MSI Availability**: Windows Installer (.msi) is **NOT AVAILABLE** in this release because the WiX Toolset is not installed in the automated build environment.
4. **Authenticode Code Signing**: Code signing is **NOT CONFIGURED**. Binaries are currently unsigned. When running for the first time, Windows SmartScreen will prompt for manual execution confirmation. Organizations should sign binaries via internal deployment pipelines.
5. **Vulnerability & CVE Scanning**: While package secret hygiene and prohibited file audits passed completely, **DEEP CVE SCAN = NOT AVAILABLE** due to the isolated offline build environment lacking live access to external CVE registry databases. Full security is **not** claimed from static file analysis alone.

---

## Distribution Artifact Inventory & SHA-256 Checksums

| Artifact | File Size | SHA-256 Hash | Description |
|---|---|---|---|
| `AirGestureAI-4.1.0-Portable.zip` | 40,782,656 B (38.89 MB) | `8c5472b8d6eb5cd05417c7bc2b76d60079287cfa173a823ed9aaeff3ec315b35` | Win-x64 portable release package |
| `Documentation.zip` | 43,456 B (42.4 KB) | `47b7c03f3abeef86f03db88832df6fff0d39b3974a646f062dc094d5166403e3` | Comprehensive user, admin, & architecture guides |
| `SDK.zip` | 9,818 B (9.59 KB) | `15a10c198de0ffd93581b17f9982d018d04d960aa39a107fb0d87e9a4042f306` | Plugin developer SDK contracts & templates |
| `SBOM.spdx.json` | 2,974 B (2.9 KB) | `5772a4aaee4a30160171ffde4d535f47e3589a54e2d3260a41ed13223d5a9958` | Software Bill of Materials (SPDX 2.3 standard) |
| `release_manifest.json` | 1,555 B (1.5 KB) | `87a319993bed9d43635654b7b364d2b3950d953bc9b6ce179a6834970827900c` | Detailed build metadata and artifact manifest |
| `release_validation_report.json`| 1,785 B (1.7 KB) | `577704f356988a91fc85c071c8285753ba1d5d08aeb720dd0ffd5bb6c0f914e6` | Automated validator execution report (6/6 passed) |
| `checksums.sha256` | 531 B | `937086eb689fee120d951237d81add4290fb10f2e419f71f0ab926319a8cef16` | Cryptographic SHA-256 index file |

---

## Quick Start Guide

1. Download and extract `AirGestureAI-4.1.0-Portable.zip`.
2. Confirm SHA-256 checksum matches `checksums.sha256`.
3. Launch `AirGestureAI.exe`.
4. Allow Windows SmartScreen prompt (*More info > Run anyway*).
5. Ensure webcam access is granted in Windows Settings.
