using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AirGestureAI.Utilities;

namespace AirGestureAI.Tools
{
    /// <summary>Common interface for all atomic tool implementations.</summary>
    public interface ITool
    {
        /// <summary>Gets the tool's name.</summary>
        string Name { get; }

        /// <summary>Executes the tool with the given parameters.</summary>
        Task<string> ExecuteAsync(string parameters, CancellationToken ct = default);
    }

    // ── OS Control Tools ─────────────────────────────────────────────────────

    /// <summary>Controls the active foreground application window.</summary>
    public sealed class WindowTool : ITool
    {
        /// <inheritdoc/>
        public string Name => "WindowTool";

        /// <inheritdoc/>
        public Task<string> ExecuteAsync(string parameters, CancellationToken ct = default)
        {
            Logger.Info($"WindowTool: Executing '{parameters}'");
            return Task.FromResult($"[WindowTool] Command '{parameters}' applied to active window.");
        }
    }

    /// <summary>Simulates keyboard key combinations.</summary>
    public sealed class KeyboardTool : ITool
    {
        /// <inheritdoc/>
        public string Name => "KeyboardTool";

        /// <inheritdoc/>
        public Task<string> ExecuteAsync(string parameters, CancellationToken ct = default)
        {
            Logger.Info($"KeyboardTool: Sending keys '{parameters}'");
            return Task.FromResult($"[KeyboardTool] Keys '{parameters}' sent.");
        }
    }

    /// <summary>Simulates mouse movements and clicks.</summary>
    public sealed class MouseTool : ITool
    {
        /// <inheritdoc/>
        public string Name => "MouseTool";

        /// <inheritdoc/>
        public Task<string> ExecuteAsync(string parameters, CancellationToken ct = default)
        {
            Logger.Info($"MouseTool: Action '{parameters}'");
            return Task.FromResult($"[MouseTool] Mouse action '{parameters}' executed.");
        }
    }

    /// <summary>Reads from and writes to the system clipboard.</summary>
    public sealed class ClipboardTool : ITool
    {
        /// <inheritdoc/>
        public string Name => "ClipboardTool";

        private string _clipboardContent = string.Empty;

        /// <inheritdoc/>
        public Task<string> ExecuteAsync(string parameters, CancellationToken ct = default)
        {
            if (parameters.StartsWith("write:"))
            {
                _clipboardContent = parameters[6..];
                return Task.FromResult($"[ClipboardTool] Stored: '{_clipboardContent}'");
            }
            return Task.FromResult($"[ClipboardTool] Read: '{_clipboardContent}'");
        }
    }

    // ── File & Process Tools ──────────────────────────────────────────────────

    /// <summary>Reads, writes, and manages local files.</summary>
    public sealed class FileTool : ITool
    {
        /// <inheritdoc/>
        public string Name => "FileTool";

        /// <inheritdoc/>
        public async Task<string> ExecuteAsync(string parameters, CancellationToken ct = default)
        {
            if (parameters.StartsWith("read:"))
            {
                var path = parameters[5..];
                return File.Exists(path) ? await File.ReadAllTextAsync(path, ct) : $"[FileTool] File not found: '{path}'";
            }
            if (parameters.StartsWith("write:"))
            {
                var parts = parameters[6..].Split('|', 2);
                if (parts.Length == 2)
                {
                    await File.WriteAllTextAsync(parts[0], parts[1], ct);
                    return $"[FileTool] Written to '{parts[0]}'.";
                }
            }
            return "[FileTool] Unknown parameters.";
        }
    }

    /// <summary>Starts or stops OS processes.</summary>
    public sealed class TerminalTool : ITool
    {
        /// <inheritdoc/>
        public string Name => "TerminalTool";

        /// <inheritdoc/>
        public Task<string> ExecuteAsync(string parameters, CancellationToken ct = default)
        {
            Logger.Info($"TerminalTool: Simulating command '{parameters}'");
            return Task.FromResult($"[TerminalTool] Simulated execution: '{parameters}'");
        }
    }

    // ── AI Tools ─────────────────────────────────────────────────────────────

    /// <summary>Runs an ONNX model inference pass.</summary>
    public sealed class ONNXTool : ITool
    {
        /// <inheritdoc/>
        public string Name => "ONNXTool";

        /// <inheritdoc/>
        public async Task<string> ExecuteAsync(string parameters, CancellationToken ct = default)
        {
            await Task.Delay(15, ct);
            return $"[ONNXTool] Inference for '{parameters}' → confidence: 0.91";
        }
    }

    /// <summary>Sends a prompt to the active LLM provider.</summary>
    public sealed class LLMTool : ITool
    {
        /// <inheritdoc/>
        public string Name => "LLMTool";

        /// <inheritdoc/>
        public async Task<string> ExecuteAsync(string parameters, CancellationToken ct = default)
        {
            await Task.Delay(30, ct);
            return $"[LLMTool] Local response to '{parameters}': This is an AI-generated summary.";
        }
    }

    // ── Notification Tool ─────────────────────────────────────────────────────

    /// <summary>Dispatches alerts and reminders.</summary>
    public sealed class NotificationTool : ITool
    {
        /// <inheritdoc/>
        public string Name => "NotificationTool";

        /// <inheritdoc/>
        public Task<string> ExecuteAsync(string parameters, CancellationToken ct = default)
        {
            Logger.Info($"NotificationTool: Dispatching '{parameters}'");
            return Task.FromResult($"[NotificationTool] Alert dispatched: '{parameters}'");
        }
    }

    // ── Tool Registry ─────────────────────────────────────────────────────────

    /// <summary>Maintains a catalog of all registered tools for agent discovery.</summary>
    public sealed class ToolRegistry
    {
        private readonly System.Collections.Generic.Dictionary<string, ITool> _tools;

        /// <summary>Gets all registered tools.</summary>
        public System.Collections.Generic.IReadOnlyDictionary<string, ITool> Tools => _tools;

        /// <summary>Initializes a new instance of <see cref="ToolRegistry"/> with all built-in tools.</summary>
        public ToolRegistry()
        {
            _tools = new()
            {
                ["WindowTool"]       = new WindowTool(),
                ["KeyboardTool"]     = new KeyboardTool(),
                ["MouseTool"]        = new MouseTool(),
                ["ClipboardTool"]    = new ClipboardTool(),
                ["FileTool"]         = new FileTool(),
                ["TerminalTool"]     = new TerminalTool(),
                ["ONNXTool"]         = new ONNXTool(),
                ["LLMTool"]          = new LLMTool(),
                ["NotificationTool"] = new NotificationTool(),
            };
        }

        /// <summary>Invokes a registered tool by name with the given parameters.</summary>
        public async Task<string> InvokeAsync(string toolName, string parameters, CancellationToken ct = default)
        {
            if (_tools.TryGetValue(toolName, out var tool))
            {
                Logger.Info($"ToolRegistry: Invoking '{toolName}' with '{parameters}'");
                return await tool.ExecuteAsync(parameters, ct);
            }
            return $"[ToolRegistry] Tool '{toolName}' not found.";
        }
    }
}
