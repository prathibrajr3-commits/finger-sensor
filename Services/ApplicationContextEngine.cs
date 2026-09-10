using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AirGestureAI.Models;
using AirGestureAI.Utilities;

namespace AirGestureAI.Services
{
    /// <summary>
    /// Polls the Windows foreground window at a configurable interval,
    /// classifies the active application, and raises <see cref="ApplicationChanged"/>
    /// events when the context or window title changes.
    /// </summary>
    public sealed class ApplicationContextEngine : IApplicationContextEngine
    {
        // ── Win32 P/Invokes ──────────────────────────────────────────────────
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern int GetWindowThreadProcessId(IntPtr hwnd, out int lpdwProcessId);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int count);
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hwnd);

        // ── Configuration ────────────────────────────────────────────────────
        private const int PollIntervalMs = 150;
        private const int TitleBufferSize = 512;

        // ── State ────────────────────────────────────────────────────────────
        private CancellationTokenSource? _cts;
        private Task? _pollingTask;
        private ApplicationContext _currentContext = ApplicationContext.Empty;

        // Cache process info to avoid repeated disk/process queries
        private readonly Dictionary<int, (string name, string path)> _processCache = new();
        private readonly object _cacheLock = new object();

        // Throttle identical "Unknown" logs
        private string _lastUnknownProcess = string.Empty;
        private DateTime _lastUnknownLogTime = DateTime.MinValue;

        // ── Public API ───────────────────────────────────────────────────────
        public event EventHandler<ApplicationChangedEventArgs>? ApplicationChanged;

        public ApplicationContext CurrentContext
        {
            get { lock (_cacheLock) { return _currentContext; } }
        }

        public void Start()
        {
            if (_pollingTask != null && !_pollingTask.IsCompleted) return;

            Logger.Info("ApplicationContextEngine starting...");
            _cts = new CancellationTokenSource();
            _pollingTask = Task.Run(() => PollLoopAsync(_cts.Token), _cts.Token);
        }

        public void Stop()
        {
            Logger.Info("ApplicationContextEngine stopping...");
            _cts?.Cancel();
            try { _pollingTask?.Wait(TimeSpan.FromSeconds(2)); } catch { /* expected on cancel */ }
            _cts = null;
            _pollingTask = null;
        }

        public void Dispose() => Stop();

        // ── Polling Loop ─────────────────────────────────────────────────────

        private async Task PollLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    PollForegroundWindow();
                }
                catch (Exception ex)
                {
                    Logger.Error("ApplicationContextEngine poll error", ex);
                }

                try
                {
                    await Task.Delay(PollIntervalMs, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
            Logger.Info("ApplicationContextEngine poll loop exited.");
        }

        private void PollForegroundWindow()
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero || !IsWindow(hwnd)) return;

            GetWindowThreadProcessId(hwnd, out int pid);
            if (pid == 0) return;

            // Get window title
            var sb = new StringBuilder(TitleBufferSize);
            GetWindowText(hwnd, sb, TitleBufferSize);
            string title = sb.ToString();

            // Get process info (cached by PID)
            var (procName, exePath) = GetProcessInfo(pid);

            // Classify
            ApplicationType appType = Classify(procName, title);

            // Build new context
            var newContext = new ApplicationContext(appType, procName, exePath, title, hwnd, pid);

            ApplicationContext previous;
            lock (_cacheLock) { previous = _currentContext; }

            // Detect changes
            bool pidChanged = previous.ProcessId != pid || previous.WindowHandle != hwnd;
            bool titleChanged = previous.WindowTitle != title;

            if (!pidChanged && !titleChanged) return; // No change — skip event

            // Update cached context
            lock (_cacheLock) { _currentContext = newContext; }

            bool titleOnly = !pidChanged && titleChanged;

            // Throttle "Unknown" log spam
            if (appType == ApplicationType.Unknown)
            {
                var now = DateTime.UtcNow;
                if (procName != _lastUnknownProcess || (now - _lastUnknownLogTime).TotalSeconds > 10)
                {
                    Logger.Info($"AppContext: Unknown application detected — process='{procName}' title='{title}'");
                    _lastUnknownProcess = procName;
                    _lastUnknownLogTime = now;
                }
            }
            else
            {
                string changeType = titleOnly ? "title change" : "app change";
                Logger.Info($"AppContext ({changeType}): {appType} | process={procName} | \"{title}\"");
            }

            // Raise event
            ApplicationChanged?.Invoke(this,
                new ApplicationChangedEventArgs(previous == ApplicationContext.Empty ? null : previous, newContext, titleOnly));
        }

        // ── Process Info Cache ────────────────────────────────────────────────

        private (string name, string path) GetProcessInfo(int pid)
        {
            lock (_cacheLock)
            {
                if (_processCache.TryGetValue(pid, out var cached)) return cached;
            }

            string name = string.Empty;
            string path = string.Empty;

            try
            {
                var proc = Process.GetProcessById(pid);
                name = proc.ProcessName.ToLowerInvariant();
                try { path = proc.MainModule?.FileName ?? string.Empty; } catch { /* Access denied for system processes */ }
            }
            catch (Exception ex)
            {
                Logger.Warn($"AppContext: Could not query PID {pid}: {ex.Message}");
            }

            var info = (name, path);

            lock (_cacheLock)
            {
                // Evict cache if too large (prevent unbounded growth across session)
                if (_processCache.Count > 200) _processCache.Clear();
                _processCache[pid] = info;
            }

            return info;
        }

        // ── Application Classifier ────────────────────────────────────────────

        private static ApplicationType Classify(string processName, string windowTitle)
        {
            return processName switch
            {
                "chrome"     => ApplicationType.Chrome,
                "msedge"     => ApplicationType.Edge,
                "firefox"    => ApplicationType.Firefox,
                "brave"      => ApplicationType.Brave,
                "vlc"        => ApplicationType.Vlc,
                "spotify"    => ApplicationType.Spotify,
                "powerpnt"   => ApplicationType.PowerPoint,
                "acrord32"   => ApplicationType.AdobeAcrobat,
                "acrobat"    => ApplicationType.AdobeAcrobat,
                "explorer"   => windowTitle.Equals("Program Manager", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(windowTitle)
                                    ? ApplicationType.WindowsDesktop
                                    : ApplicationType.FileExplorer,
                _            => ApplicationType.Unknown
            };
        }
    }
}
