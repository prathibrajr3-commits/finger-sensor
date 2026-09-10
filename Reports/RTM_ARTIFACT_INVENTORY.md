# AirGesture AI v4.1.0 RTM — Physical Artifact Inventory

**Inventory Timestamp**: 2026-09-10T19:00:00+05:30  
**Directory**: `dist\`  
**Updated By**: Automated Release Packaging & Verification Run  
**Target Architecture**: win-x64 (.NET 8.0-windows)  

---

## Physical Files in `dist\` Directory

| Filename | Exists | File Size | Classification | Validation Status | SHA-256 Checksum | Notes |
|---|---|---|---|---|---|---|
| `AirGestureAI-4.1.0-Portable.zip` | **YES** | 40,782,656 B (38.89 MB) | Required | `VERIFIED` | `8c5472b8d6eb5cd05417c7bc2b76d60079287cfa173a823ed9aaeff3ec315b35` | Runtime-pruned win-x64 portable package |
| `Documentation.zip` | **YES** | 43,456 B (42.4 KB) | Required | `VERIFIED` | `47b7c03f3abeef86f03db88832df6fff0d39b3974a646f062dc094d5166403e3` | Release documentation package |
| `SDK.zip` | **YES** | 9,818 B (9.59 KB) | Required | `VERIFIED` | `15a10c198de0ffd93581b17f9982d018d04d960aa39a107fb0d87e9a4042f306` | Developer SDK & plugin interfaces |
| `SBOM.spdx.json` | **YES** | 2,974 B (2.9 KB) | Required | `VERIFIED` | `5772a4aaee4a30160171ffde4d535f47e3589a54e2d3260a41ed13223d5a9958` | Software Bill of Materials (SPDX 2.3) |
| `checksums.sha256` | **YES** | 531 B | Required | `VERIFIED` | *(Manifest index file)* | SHA-256 checksum index of all release artifacts |
| `release_manifest.json` | **YES** | 1,555 B (1.5 KB) | Required | `VERIFIED` | `87a319993bed9d43635654b7b364d2b3950d953bc9b6ce179a6834970827900c` | Release metadata & payload manifest |
| `release_validation_report.json` | **YES** | 1,785 B (1.7 KB) | Required | `VERIFIED` | `577704f356988a91fc85c071c8285753ba1d5d08aeb720dd0ffd5bb6c0f914e6` | Release validator report (6/6 passed) |
| `AirGestureAI-4.1.0-Setup.msi` | **NO** | — | Optional | `NOT AVAILABLE` | — | WiX Toolset not installed on host machine |

---

## Summary of Artifact Physical Presence

- **Present & Verified (7 files)**:
  - [AirGestureAI-4.1.0-Portable.zip](file:///e:/finger%20sensor/dist/AirGestureAI-4.1.0-Portable.zip) (38.89 MB) — win-x64 portable package, 44 verified entries, no prohibited files, credentials, secrets, or paths
  - [Documentation.zip](file:///e:/finger%20sensor/dist/Documentation.zip) (42.4 KB) — User and integration guides
  - [SDK.zip](file:///e:/finger%20sensor/dist/SDK.zip) (9.59 KB) — SDK contracts and sample plugin templates
  - [SBOM.spdx.json](file:///e:/finger%20sensor/dist/SBOM.spdx.json) (2.9 KB) — SPDX 2.3 bill of materials
  - [checksums.sha256](file:///e:/finger%20sensor/dist/checksums.sha256) (531 B) — Verified SHA-256 hashes
  - [release_manifest.json](file:///e:/finger%20sensor/dist/release_manifest.json) (1.5 KB) — Manifest specifying v4.1.0, win-x64, net8.0-windows
  - [release_validation_report.json](file:///e:/finger%20sensor/dist/release_validation_report.json) (1.7 KB) — Validator report (6/6 checks passed)
- **Absent (1 file)**:
  - `AirGestureAI-4.1.0-Setup.msi` (Optional) — Requires WiX Toolset (candle/light); portable package is primary distribution vehicle

---

## Inventory Compliance

- All 7 listed physical files exist on disk and have verified SHA-256 checksums matching `dist\checksums.sha256`.
- No placeholder or dummy ZIP/MSI binaries have been placed into `dist\`.
- Portable distribution contains verified binaries compiled for `win-x64` with non-Windows runtimes pruned.
