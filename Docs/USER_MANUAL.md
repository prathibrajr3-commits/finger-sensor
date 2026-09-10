# AirGesture AI v4.0.0 — User Manual

Welcome to AirGesture AI! This manual helps you configure, calibrate, and use touchless air gestures to control your Windows applications.

---

## 1. Quick Start

### Hardware Requirements
- A standard USB webcam or integrated laptop camera (720p at 30 FPS recommended).
- Hardware with DirectX 12/DirectML or NVIDIA GPU support (highly recommended for neural inference acceleration).

### Startup Instructions
1. Launch `AirGestureAI.exe`.
2. The application starts in the system tray and opens the main dashboard.
3. Select your webcam from the **Settings** or **Camera** tab.
4. Click **Start Tracking**. The status LED turns blue, indicating tracking is active.

---

## 2. Gesture Guide

AirGesture AI supports the following standard gestures out-of-the-box:

- **Scroll Up**: Raise your hand and swipe upward to scroll active application windows up.
- **Scroll Down**: Swipe downward to scroll active application windows down.
- **Open Palm**: Press and release the Spacebar (ideal for pausing/playing YouTube videos or media players).

---

## 3. Calibration Wizard

If coordinates are offset or cursor movement feels unstable:
1. Navigate to the **Spatial** tab.
2. Select **Run Stereo Calibration**.
3. Follow the visual prompts to align your camera lenses with your physical desktop bounds.
4. Once completed, calibration will show **Calibrated**.

---

## 4. Telemetry and Analytics

The **Edge AI** and **Telemetry** tabs display live diagnostic details:
- **Inference Latency**: Processing delay for AI worker model evaluations.
- **Tracking FPS**: Real-time camera frames processed per second (Target: 30 Hz).
- **Active Anchors**: Physical spatial points calibrated for context retention.
