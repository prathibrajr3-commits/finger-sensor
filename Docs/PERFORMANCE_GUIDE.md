# AirGesture AI v4.1 RTM — Performance & Diagnostics Guide

This document describes the design, architectures, and report structures of the telemetry and diagnostics subsystems implemented in AirGesture AI v4.1.

---

## Table of Contents

1. [Diagnostics Architecture](#1-diagnostics-architecture)
2. [Performance Profiler Service](#2-performance-profiler-service)
3. [Memory Diagnostics Service](#3-memory-diagnostics-service)
4. [Accessibility Verifier Service](#4-accessibility-verifier-service)
5. [System & Plugin Diagnostics](#5-system--plugin-diagnostics)
6. [Diagnostics Manager](#6-diagnostics-manager)
7. [Report Schemas](#7-report-schemas)
8. [Performance Tuning Recommendations](#8-performance-tuning-recommendations)

---

## 1. Diagnostics Architecture

AirGesture AI utilizes a decoupled diagnostics architecture where individual agents gather metrics and output structured JSON telemetry reports:

```
  Telemetry Sources (Mats, CPU, UI, Pipes, ONNX)
                     │
                     ▼
  ┌────────────────────────────────────────────────────────┐
  │              Orchestrated Services                     │
  │                                                        │
  │  • PerformanceProfilerService (CPU/RAM/Latencies)     │
  │  • MemoryDiagnosticsService   (Leak tracking)          │
  │  • AccessibilityVerifierService (WPF Visual Tree check)│
  │  • SystemDiagnosticsService   (Hardware / OS details)  │
  │  • PluginDiagnosticsService   (Load state metrics)     │
  └──────────────────────────┬─────────────────────────────┘
                             │
                             ▼
  ┌────────────────────────────────────────────────────────┐
  │                 DiagnosticsManager                     │
  │  • Runs scheduled sweeps every 30 seconds               │
  │  • Coordinates thread-safe writes                      │
  │  • Manages export directory location                   │
  └──────────────────────────┬─────────────────────────────┘
                             │ Writes reports to
                             ▼
  %LocalAppData%\AirGestureAI\Diagnostics\
       ├── performance_report.json
       ├── memory_report.json
       ├── accessibility_report.json
       ├── system_report.json
       └── plugin_report.json
```

---

## 2. Performance Profiler Service

`PerformanceProfilerService` collects runtime telemetry every second and maintains a rolling 1-hour history (3,600 samples).

### Telemetry Collected

| Telemetry | Collection Method / Source |
|---|---|
| CPU Usage % | Process total processor time differential / CPU logical count |
| RAM Memory MB | `Process.WorkingSet64` |
| GPU Load % | WMI Video Controller performance query |
| GC Collections | `GC.CollectionCount(gen)` for Gen 0, 1, 2 |
| GC Pauses | `GC.GetTotalPauseDuration()` |
| ThreadPool Queue | Pending work item calculations |
| Latencies (ms) | Real-time queue telemetry (IPC, Frame, Gesture, AI, Plugin) |

---

## 3. Memory Diagnostics Service

Provides leak detection by maintaining a thread-safe registry of active references wrapped in `WeakReference`.

### Usage

```csharp
// Register resource
MemoryDiag.Track(myOpenCvMat, "OpenCV Mats");

// Release resource
MemoryDiag.Untrack(myOpenCvMat);
myOpenCvMat.Dispose();
```

### Leak Evaluation Criteria

An object is flagged as a potential leak if it has been tracked for **over 10 seconds** and has not been untracked or collected by the garbage collector.

---

## 4. Accessibility Verifier Service

Traverses the active WPF Visual Tree recursively on the Dispatcher thread to verify accessibility standards.

### Validation Checks

1. **Tab Order**: Focusable controls must have `TabIndex` or `KeyboardNavigation` configured.
2. **Automation ID**: UI elements must define `AutomationProperties.AutomationId` for automated test suites.
3. **Automation Name**: Focusable controls must define `AutomationProperties.Name` for screen readers.
4. **Focus Visuals**: Focusable controls must define `FocusVisualStyle`.
5. **System Settings**: Detects System parameters for High Contrast, Reduced Motion, and Screen Reader status.

---

## 5. System & Plugin Diagnostics

### System Diagnostics

Queries the local machine configuration using standard .NET environment parameters and Windows Management Instrumentation (WMI):
- OS platform description, architecture, and .NET Framework version.
- Hardware specifications: CPU core count, active GPU name, total physical RAM.
- Display specifications: Primary monitor width and height.

### Plugin Diagnostics

Collects dynamic status statistics from the `PluginManager`:
- Counts of loaded vs. failed plugins.
- Load duration logs.
- Assembly directories.
- Deserialized error logs for plugins that failed to initialize.

---

## 6. Diagnostics Manager

The central orchestrator registered as a singleton. It is resolved in `App.xaml.cs` and triggers a sweep of all diagnostic services every **30 seconds** in a non-blocking background task.

---

## 7. Report Schemas

All reports are written in camelCase JSON format.

### performance_report.json

```json
{
  "timestamp": "2026-08-06T05:30:00.0000000Z",
  "totalSamplesCollected": 120,
  "averages": {
    "cpuUsagePercent": 14.2,
    "memoryMb": 185.3,
    "gpuUsagePercent": 8.0,
    "ipcLatencyMs": 4.1,
    "frameLatencyMs": 18.3,
    "gestureLatencyMs": 2.2,
    "aiResponseMs": 28.5
  },
  "max": {
    "cpuUsagePercent": 75.0,
    "memoryMb": 210.0,
    "gpuUsagePercent": 15.0,
    "ipcLatencyMs": 45.0,
    "frameLatencyMs": 35.0,
    "gestureLatencyMs": 12.0,
    "aiResponseMs": 120.0
  },
  "p95": {
    "cpuUsagePercent": 45.0,
    "memoryMb": 195.0,
    "gpuUsagePercent": 12.0,
    "ipcLatencyMs": 12.0,
    "frameLatencyMs": 28.0,
    "gestureLatencyMs": 5.0,
    "aiResponseMs": 60.0
  },
  "hotspots": [
    "Frame processing latency exceeded 33.3ms (dropped frame threat)."
  ],
  "recommendations": [
    "Check camera frame capture resolution and reduce OpenCV frame scaling overhead."
  ]
}
```

---

## 8. Performance Tuning Recommendations

### CPU Optimization
If average CPU exceeds **40%**, reduce ONNX Runtime execution provider threads in `OnnxRuntimeEngine.Initialize()` or adjust capture rate to 30 FPS.

### Memory Leaks
If average RAM exceeds the **250MB** RTM target threshold:
1. Run `MemoryDiagnosticsService` report to identify the leaking category.
2. Force `PerformanceOptimizer.TrimCaches()` to release cached bitmap resources and prompt garbage collection.

### Drop Frame Issues
If average frame latency exceeds **33.3ms**, the pipeline is dropping frames. Optimize OpenCV frame scaling parameters or use the DirectML execution provider to offload landmark processing to the GPU.
