# AirGesture AI Changelog

All notable changes to the AirGesture AI project will be documented in this file.

---

## [4.0.0] - 2026-08-03

### Added
- Out-of-process isolation mode (`--tracker-host` and `--ai-worker` process arguments).
- **ServiceHost** subprocess coordinator and lifecycle manager.
- Secure Named Pipe IPC loop (`IpcServer`, `IpcClient`, `IpcRouter`).
- Integrated `Microsoft.ML.OnnxRuntime` for local neural network execution.
- Added support for DirectML and CUDA GPU execution providers in ONNX inference.
- Cosine-similarity intent classifier using character word-hashing embeddings.
- Kahn's algorithm topological sorting DAG planner with concurrent tier scheduling and task retries.
- DPAPI-protected credentials storage vault (`SecureVault`).
- Enterprise-grade Policy Engine and tamper-proof security `AuditLogger`.
- Low-level keyboard and mouse simulation replayer.
- Live performance profiling dashboard integration.

### Changed
- Refactored `IpcSerializer` to use standard `System.Text.Json` instead of substring extraction.
- Replaced stub-based topological sorting inside `DependencyGraph` with a production Kahn's algorithm implementation.
- Refactored `ProductionHealthMonitor.RunHealthSweep` to clear previous results, avoiding heap memory accumulation.
- Switched `CpuProfiler.UsagePercent` from `new Random()` to `Random.Shared` to avoid GC allocation pressure.

### Fixed
- Fixed missing `"PluginInstall"` rule definition inside `PolicyEngine` causing vault tests to fail.
- Fixed duplicate `GestureSequenceEngine` registration inside the WPF dependency injection startup block.
- Fixed a regression in `ArchitectureTests.TestIpcLoopAsync` where the test router lacked a `"Ping"` handler.

---

## [3.5.0] - 2026-04-12
- Initial WPF architecture dashboard presentation.
- Added virtual cursor engine and hover click capabilities.
- Native OpenCV camera capture frame providers.
- Basic keyword-based intent classification.
