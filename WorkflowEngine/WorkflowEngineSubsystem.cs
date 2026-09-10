using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AirGestureAI.Utilities;

namespace AirGestureAI.WorkflowEngine
{
    // ── Workflow Models ───────────────────────────────────────────────────────

    /// <summary>Represents a single executable action in a workflow.</summary>
    public sealed class WorkflowStep
    {
        /// <summary>Gets or sets the action name (e.g. "Keyboard", "MouseClick", "ForegroundWindow").</summary>
        public string Action { get; set; } = string.Empty;

        /// <summary>Gets or sets the target tool or agent for this step.</summary>
        public string Target { get; set; } = string.Empty;

        /// <summary>Gets or sets the execution parameters.</summary>
        public string Parameters { get; set; } = string.Empty;

        /// <summary>Gets or sets the status of this step.</summary>
        public string Status { get; set; } = "Pending";
    }

    /// <summary>Represents a saved workflow sequence with trigger binding.</summary>
    public sealed class Workflow
    {
        /// <summary>Gets the unique workflow identifier.</summary>
        public string WorkflowId { get; } = Guid.NewGuid().ToString()[..8];

        /// <summary>Gets or sets the workflow name.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets or sets the trigger gesture or command.</summary>
        public string Trigger { get; set; } = string.Empty;

        /// <summary>Gets the list of steps in this workflow.</summary>
        public List<WorkflowStep> Steps { get; } = new();

        /// <summary>Gets or sets the creation timestamp.</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>Predefined workflow blueprints (e.g. "Meeting Mode", "Coding Setup").</summary>
    public sealed class WorkflowTemplate
    {
        /// <summary>Gets or sets the template name.</summary>
        public string TemplateName { get; set; } = string.Empty;

        /// <summary>Gets the default steps for this template.</summary>
        public List<WorkflowStep> DefaultSteps { get; } = new();

        /// <summary>Creates a live <see cref="Workflow"/> from this template.</summary>
        public Workflow Instantiate()
        {
            var wf = new Workflow { Name = TemplateName, Trigger = TemplateName.ToLower() };
            foreach (var step in DefaultSteps)
                wf.Steps.Add(new WorkflowStep { Action = step.Action, Target = step.Target, Parameters = step.Parameters });
            return wf;
        }
    }

    // ── Win32 low-level Hooks & SendInput Replayer ──────────────────────────────

    /// <summary>
    /// Records user keyboard, mouse, and active window transitions using low-level Win32 Hooks.
    /// </summary>
    public sealed class WorkflowRecorder
    {
        private Workflow? _recording;
        private bool _isRecording;
        private IntPtr _keyboardHookId = IntPtr.Zero;
        private IntPtr _mouseHookId = IntPtr.Zero;
        private IntPtr _lastActiveWindow = IntPtr.Zero;
        private string _lastClipboardText = string.Empty;

        // Keep delegates alive to prevent garbage collection crashes
        private LowLevelHookProc? _keyboardProc;
        private LowLevelHookProc? _mouseProc;

        /// <summary>Gets whether recording is currently active.</summary>
        public bool IsRecording => _isRecording;

        /// <summary>Starts a new recording session and installs Win32 low-level hooks.</summary>
        public void StartRecording(string workflowName)
        {
            if (_isRecording) return;

            _recording = new Workflow { Name = workflowName };
            _isRecording = true;

            // Initialize hook delegates
            _keyboardProc = KeyboardHookCallback;
            _mouseProc = MouseHookCallback;

            // Install low-level hooks
            _keyboardHookId = SetHook(WH_KEYBOARD_LL, _keyboardProc);
            _mouseHookId = SetHook(WH_MOUSE_LL, _mouseProc);

            Logger.Info($"WorkflowRecorder: Installed low-level Win32 Hooks. Recording '{workflowName}'...");
        }

        /// <summary>Records a single step.</summary>
        public void CaptureStep(string action, string target = "", string parameters = "")
        {
            if (!_isRecording || _recording == null) return;
            _recording.Steps.Add(new WorkflowStep { Action = action, Target = target, Parameters = parameters });
            Logger.Info($"WorkflowRecorder: Captured step: {action} | Target: {target} | Params: {parameters}");
        }

        /// <summary>Stops recording and uninstalls low-level Win32 hooks.</summary>
        public Workflow? StopRecording()
        {
            if (!_isRecording) return null;

            _isRecording = false;

            // Uninstall hooks
            if (_keyboardHookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_keyboardHookId);
                _keyboardHookId = IntPtr.Zero;
            }
            if (_mouseHookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_mouseHookId);
                _mouseHookId = IntPtr.Zero;
            }

            _keyboardProc = null;
            _mouseProc = null;

            Logger.Info($"WorkflowRecorder: Uninstalled low-level Win32 Hooks. Captured {_recording?.Steps.Count ?? 0} steps.");
            return _recording;
        }

        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                var keyStr = ((System.Windows.Input.Key)System.Windows.Input.KeyInterop.KeyFromVirtualKey(vkCode)).ToString();
                
                // Inspect active window focus
                CheckActiveWindowChange();
                
                // Inspect clipboard content
                CheckClipboardChange();

                CaptureStep("Keyboard", keyStr, vkCode.ToString());
            }
            return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_LBUTTONDOWN)
            {
                var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                CheckActiveWindowChange();
                CaptureStep("MouseClick", $"{hookStruct.pt.x},{hookStruct.pt.y}");
            }
            return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        private void CheckActiveWindowChange()
        {
            var active = GetForegroundWindow();
            if (active != _lastActiveWindow)
            {
                _lastActiveWindow = active;
                var sb = new StringBuilder(256);
                GetWindowText(active, sb, sb.Capacity);
                var title = sb.ToString();
                CaptureStep("ForegroundWindow", title);
            }
        }

        private void CheckClipboardChange()
        {
            try
            {
                // Access clipboard safely from a STA thread fallback
                string text = string.Empty;
                var thread = new Thread(() =>
                {
                    try
                    {
                        if (System.Windows.Clipboard.ContainsText())
                        {
                            text = System.Windows.Clipboard.GetText();
                        }
                    }
                    catch { }
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                thread.Join(100);

                if (!string.IsNullOrEmpty(text) && text != _lastClipboardText)
                {
                    _lastClipboardText = text;
                    // Cap text length to prevent giant memory footprints
                    var capped = text.Length > 60 ? text[..60] + "..." : text;
                    CaptureStep("ClipboardCopy", capped);
                }
            }
            catch { }
        }

        // ── Win32 P/Invokes ──────────────────────────────────────────────────────

        private delegate IntPtr LowLevelHookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelHookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        private const int WH_KEYBOARD_LL = 13;
        private const int WH_MOUSE_LL = 14;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_LBUTTONDOWN = 0x0201;

        private IntPtr SetHook(int hookType, LowLevelHookProc proc)
        {
            using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                string modName = curModule?.ModuleName ?? "AirGestureAI.exe";
                return SetWindowsHookEx(hookType, proc, GetModuleHandle(modName), 0);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }
    }

    /// <summary>
    /// Replays a saved workflow by synthesizing low-level input events using Win32 SendInput.
    /// </summary>
    public sealed class WorkflowPlayer
    {
        /// <summary>Replays all steps in a workflow asynchronously using native Win32 inputs.</summary>
        public async Task PlayAsync(Workflow workflow, CancellationToken ct = default)
        {
            Logger.Info($"WorkflowPlayer: Replaying '{workflow.Name}' ({workflow.Steps.Count} steps)...");
            
            foreach (var step in workflow.Steps)
            {
                ct.ThrowIfCancellationRequested();
                step.Status = "Running";
                
                Logger.Info($"WorkflowPlayer: Executing native step '{step.Action}' -> {step.Target}");
                
                try
                {
                    if (step.Action == "Keyboard" && int.TryParse(step.Parameters, out int vkCode))
                    {
                        SendKey((ushort)vkCode);
                    }
                    else if (step.Action == "MouseClick")
                    {
                        var parts = step.Target.Split(',');
                        if (parts.Length == 2 && int.TryParse(parts[0], out int x) && int.TryParse(parts[1], out int y))
                        {
                            SetCursorPos(x, y);
                            SendMouseClick();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"WorkflowPlayer: Failed executing step: {ex.Message}");
                }

                await Task.Delay(100, ct);
                step.Status = "Done";
            }
            Logger.Info($"WorkflowPlayer: Replay of '{workflow.Name}' completed.");
        }

        // ── Win32 SendInput P/Invokes ────────────────────────────────────────────

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetCursorPos(int x, int y);

        private const int INPUT_MOUSE = 0;
        private const int INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;

        [StructLayout(LayoutKind.Explicit)]
        private struct INPUT
        {
            [FieldOffset(0)] public int type;
            [FieldOffset(8)] public KEYBDINPUT ki;
            [FieldOffset(8)] public MOUSEINPUT mi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private static void SendKey(ushort vkCode)
        {
            var inputs = new INPUT[2];
            
            // Key Down
            inputs[0] = new INPUT { type = INPUT_KEYBOARD };
            inputs[0].ki = new KEYBDINPUT { wVk = vkCode, dwFlags = 0 };

            // Key Up
            inputs[1] = new INPUT { type = INPUT_KEYBOARD };
            inputs[1].ki = new KEYBDINPUT { wVk = vkCode, dwFlags = KEYEVENTF_KEYUP };

            SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        private static void SendMouseClick()
        {
            var inputs = new INPUT[2];

            // Left Down
            inputs[0] = new INPUT { type = INPUT_MOUSE };
            inputs[0].mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_LEFTDOWN };

            // Left Up
            inputs[1] = new INPUT { type = INPUT_MOUSE };
            inputs[1].mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_LEFTUP };

            SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
        }
    }

    // ── Optimization & History ────────────────────────────────────────────────

    /// <summary>Analyzes recorded workflows and suggests consolidation improvements.</summary>
    public sealed class WorkflowOptimizer
    {
        /// <summary>Returns optimization hints for the given workflow.</summary>
        public List<string> Analyze(Workflow workflow)
        {
            var hints = new List<string>();
            if (workflow.Steps.Count > 10)
                hints.Add("Consider splitting this workflow into two smaller macros.");
            if (workflow.Steps.FindAll(s => s.Action == workflow.Steps[0].Action).Count > 2)
                hints.Add($"Step '{workflow.Steps[0].Action}' is repeated. Consider looping.");
            if (hints.Count == 0)
                hints.Add("Workflow is well-optimized.");
            return hints;
        }
    }

    /// <summary>Logs past workflow execution outcomes.</summary>
    public sealed class WorkflowHistory
    {
        private readonly List<(DateTime, string, string)> _records = new();

        /// <summary>Gets all history records.</summary>
        public IReadOnlyList<(DateTime, string, string)> Records => _records;

        /// <summary>Records a workflow execution outcome.</summary>
        public void Record(string workflowName, string status)
            => _records.Add((DateTime.UtcNow, workflowName, status));
    }

    // ── Central Engine ────────────────────────────────────────────────────────

    /// <summary>
    /// Coordinates workflow recordings, playbacks, schedules, templates, and history.
    /// </summary>
    public sealed class WorkflowEngineService
    {
        private readonly List<Workflow> _workflows = new();
        private readonly WorkflowRecorder _recorder = new();
        private readonly WorkflowPlayer _player = new();
        private readonly WorkflowOptimizer _optimizer = new();
        private readonly WorkflowHistory _history = new();

        /// <summary>Gets all saved workflows.</summary>
        public IReadOnlyList<Workflow> Workflows => _workflows;

        /// <summary>Clears all workflows.</summary>
        public void ClearWorkflows() => _workflows.Clear();

        /// <summary>Adds a workflow manually.</summary>
        public void AddWorkflow(Workflow workflow)
        {
            if (workflow != null)
                _workflows.Add(workflow);
        }

        /// <summary>Gets the workflow history log.</summary>
        public WorkflowHistory History => _history;

        /// <summary>Gets the recorder instance.</summary>
        public WorkflowRecorder Recorder => _recorder;

        /// <summary>Starts recording a new workflow.</summary>
        public void StartRecording(string name) => _recorder.StartRecording(name);

        /// <summary>Stops recording and saves the workflow.</summary>
        public Workflow? StopRecording()
        {
            var wf = _recorder.StopRecording();
            if (wf != null) _workflows.Add(wf);
            return wf;
        }

        /// <summary>Replays a workflow by name.</summary>
        public async Task ReplayAsync(string workflowName, CancellationToken ct = default)
        {
            var wf = _workflows.Find(w => w.Name.Equals(workflowName, StringComparison.OrdinalIgnoreCase));
            if (wf == null) { Logger.Warn($"WorkflowEngine: Workflow '{workflowName}' not found."); return; }
            await _player.PlayAsync(wf, ct);
            _history.Record(workflowName, "Completed");
        }

        /// <summary>Returns optimization tips for a named workflow.</summary>
        public List<string> Optimize(string workflowName)
        {
            var wf = _workflows.Find(w => w.Name.Equals(workflowName, StringComparison.OrdinalIgnoreCase));
            return wf != null ? _optimizer.Analyze(wf) : new List<string> { "Workflow not found." };
        }

        /// <summary>Exports all workflows to a JSON summary file.</summary>
        public string ExportWorkflows(string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);
            var path = Path.Combine(outputDirectory, $"workflows_{DateTime.Now:yyyyMMdd_HHmmss}.json");
            var lines = new System.Text.StringBuilder("[");
            foreach (var wf in _workflows)
                lines.Append($"{{\"id\":\"{wf.WorkflowId}\",\"name\":\"{wf.Name}\",\"steps\":{wf.Steps.Count}}},");
            if (_workflows.Count > 0) lines.Length--;
            lines.Append("]");
            File.WriteAllText(path, lines.ToString());
            Logger.Info($"WorkflowEngine: Exported {_workflows.Count} workflows to '{path}'.");
            return path;
        }
    }
}
