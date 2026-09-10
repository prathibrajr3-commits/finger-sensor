# AirGesture AI Privacy Policy

Last Updated: August 3, 2026

AirGesture AI is built with an absolute commitment to user privacy and data security. 

---

## 1. Zero External Data Transmission
AirGesture AI is designed and implemented as an **offline-first local platform**. 
- Camera frames, hand tracking landmarks, gesture coordinates, user prompts, and local memories are **never** transmitted over the internet or sent to external servers.
- The application does not collect, track, or share personal data, telemetry, usage statistics, or crash reports with Google or any third-party entity.

---

## 2. Local-Only Processing Boundaries
- **Video Capture**: OpenCV camera streams are processed exclusively in-memory on your local machine. Frames are immediately disposed after coordinate calculations are completed.
- **Neural Network Inference**: ONNX Model executions are run locally using Microsoft.ML.OnnxRuntime. No API keys or cloud services are required or contacted.
- **IPC Architecture**: Communication between the GUI and background subprocesses is routed over local Windows Named Pipes. Authentication tokens prevent unauthorized local software from reading this data.

---

## 3. Secure Local Storage
- Credentials, settings, and secrets are stored in a local physical vault encrypted using Windows Data Protection API (DPAPI) tied to the active Windows User Scope. 
- Log files (`airgesture_log.txt`) are stored locally in the application directory. No sensitive variables (keys, raw tokens, or passwords) are written to the logs.
