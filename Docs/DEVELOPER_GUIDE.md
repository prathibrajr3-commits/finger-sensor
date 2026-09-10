# AirGesture AI v4.0.0 — Developer Guide

This guide describes how to extend and integrate with the AirGesture AI v4.0.0 platform. It covers plugin development, Dependency Injection registration, and SDK usage.

---

## 1. Directory Structure

- `/AIWorker` — ONNX execution provider logic and subprocess host.
- `/ApiDocs` — API documentation generator scripts.
- `/Camera` — OpenCV camera provider capturing raw frames.
- `/Docs` — System architecture and developer specifications.
- `/IPC` — Named pipe messaging infrastructure.
- `/Plugins` — Active assembly adapters and smart overrides.
- `/SDK` — ZIP starter project templates and solution creators.
- `/Security` — DPAPI encrypted SecureVault, policy engine, and audit logs.
- `/Views` — WPF dashboard UI windows.

---

## 2. Writing a Custom Plugin

AirGesture AI features a pluggable framework allowing developers to override gestures or add new actions.

To create a new plugin, implement the `IPlugin` interface:

```csharp
using AirGestureAI.Plugins;
using AirGestureAI.Models;

namespace MyCustomNamespace
{
    public class MyGesturePlugin : IPlugin
    {
        public string Name => "MyCustomPlugin";
        public string Version => "1.0.0";

        public void Initialize(IPluginContext context)
        {
            // Set up local dependencies, load configuration
        }

        public bool HandleGesture(GestureType gesture, string activeApplication)
        {
            if (gesture == GestureType.OpenPalm && activeApplication == "Chrome")
            {
                // Perform custom action
                return true; // Mark as handled to prevent fallback
            }
            return false;
        }
    }
}
```

---

## 3. Dependency Injection (DI) Registration

All services must be registered in the Microsoft DI container inside `App.xaml.cs` in the `ConfigureServices` method:

```csharp
private void ConfigureServices(IServiceCollection services)
{
    // ... Existing registration blocks
    
    // Register your service
    services.AddSingleton<IMyCustomService, MyCustomService>();
}
```

Ensure constructor injection is used for resolving dependencies:

```csharp
public class MyController
{
    private readonly IMyCustomService _customService;

    public MyController(IMyCustomService customService)
    {
        _customService = customService;
    }
}
```

---

## 4. Using the SdkGenerator

The `SdkGenerator` class automatically bundles ready-to-compile solutions in ZIP format:

```csharp
var generator = new SdkGenerator();
string zipPath = generator.Generate("GesturePlugin", "OutputDirectory", "CustomGesturePlugin");
```
This generates a starter project template with:
- Main solution file (`CustomGesturePlugin.sln`)
- Plugin project configuration (`CustomGesturePlugin.csproj`)
- Sample source code (`Plugin.cs`)
- NUnit-compatible test runner stubs (`PluginTests.cs`)
- Build scripts and documentation README.
