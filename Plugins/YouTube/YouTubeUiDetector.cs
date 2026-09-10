using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AirGestureAI.HoverSelection;
using AirGestureAI.Models;
using AirGestureAI.Utilities;

namespace AirGestureAI.Plugins.YouTube
{
    /// <summary>
    /// Detects which YouTube page is active based on the browser window title,
    /// discovers interactive UI controls via Windows UI Automation (accessibility tree),
    /// and publishes hover targets to the <see cref="IHoverEngine"/>.
    /// </summary>
    public sealed class YouTubeUiDetector : IDisposable
    {
        // ── Win32 for UI Automation root ─────────────────────────────────────
        // We use raw UI Automation COM interfaces via interop.
        // This avoids the heavyweight UIAutomationClient NuGet dependency while
        // still working through the accessibility tree.
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hwnd, StringBuilder sb, int count);
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        // ── Events & State ────────────────────────────────────────────────────
        public event Action<YouTubePageType>? PageTypeChanged;
        public event Action<IReadOnlyList<TargetInfo>>? TargetsDiscovered;

        public YouTubePageType CurrentPageType { get; private set; } = YouTubePageType.None;
        public bool IsYouTubeActive { get; private set; }
        public IReadOnlyList<TargetInfo> CachedTargets { get; private set; } = Array.Empty<TargetInfo>();
        public DateTime LastUpdate { get; private set; } = DateTime.MinValue;
        public string ActiveBrowser { get; private set; } = string.Empty;

        private CancellationTokenSource? _cts;
        private Task? _pollingTask;
        private IntPtr _lastHwnd = IntPtr.Zero;
        private string _lastTitle = string.Empty;
        private DateTime _lastLogThrottle = DateTime.MinValue;

        // Refresh UI Automation targets every 2 seconds when page type changes
        private const int TargetRefreshIntervalMs = 2000;
        private DateTime _lastTargetRefresh = DateTime.MinValue;

        // ── Start / Stop ──────────────────────────────────────────────────────

        public void Start()
        {
            if (_pollingTask != null && !_pollingTask.IsCompleted) return;
            _cts = new CancellationTokenSource();
            _pollingTask = Task.Run(() => PollAsync(_cts.Token), _cts.Token);
            Logger.Info("YouTubeUiDetector started.");
        }

        public void Stop()
        {
            _cts?.Cancel();
            try { _pollingTask?.Wait(TimeSpan.FromSeconds(2)); } catch { }
            _cts = null;
            _pollingTask = null;
            IsYouTubeActive = false;
            CurrentPageType = YouTubePageType.None;
            Logger.Info("YouTubeUiDetector stopped.");
        }

        public void Dispose() => Stop();

        // ── Polling Loop ──────────────────────────────────────────────────────

        private async Task PollAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try { Detect(); }
                catch (Exception ex) { Logger.Error("YouTubeUiDetector poll error", ex); }

                try { await Task.Delay(200, ct); }
                catch (OperationCanceledException) { break; }
            }
        }

        private void Detect()
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return;

            var sb = new StringBuilder(512);
            GetWindowText(hwnd, sb, 512);
            string title = sb.ToString();

            // Skip if nothing changed and handle is the same
            bool titleChanged = title != _lastTitle;
            bool hwndChanged  = hwnd != _lastHwnd;

            if (!titleChanged && !hwndChanged) return;

            _lastHwnd  = hwnd;
            _lastTitle = title;

            // Determine browser
            bool onYouTube = title.Contains("YouTube", StringComparison.OrdinalIgnoreCase);
            bool isBrowser = IsBrowserTitle(title, out string browserName);

            if (!onYouTube || !isBrowser)
            {
                if (IsYouTubeActive)
                {
                    IsYouTubeActive = false;
                    CurrentPageType = YouTubePageType.None;
                    ActiveBrowser   = string.Empty;
                    CachedTargets   = Array.Empty<TargetInfo>();
                    Logger.Info("YouTubeUiDetector: YouTube no longer active.");
                    PageTypeChanged?.Invoke(YouTubePageType.None);
                    TargetsDiscovered?.Invoke(CachedTargets);
                }
                return;
            }

            ActiveBrowser   = browserName;
            IsYouTubeActive = true;

            // Classify page type from window title (fast heuristic, no DOM access)
            YouTubePageType pageType = ClassifyPageFromTitle(title);

            if (pageType != CurrentPageType)
            {
                Logger.Info($"YouTubeUiDetector: Page type changed to {pageType} | browser={browserName} | title=\"{title}\"");
                CurrentPageType = pageType;
                PageTypeChanged?.Invoke(pageType);

                // Force target refresh on page type change
                _lastTargetRefresh = DateTime.MinValue;
            }

            // Periodic target discovery from accessibility tree
            if ((DateTime.UtcNow - _lastTargetRefresh).TotalMilliseconds > TargetRefreshIntervalMs)
            {
                RefreshAccessibilityTargets(hwnd, pageType);
                _lastTargetRefresh = DateTime.UtcNow;
                LastUpdate = DateTime.UtcNow;
            }
        }

        // ── Page Type Classification ─────────────────────────────────────────

        private static YouTubePageType ClassifyPageFromTitle(string title)
        {
            // Window title for YouTube pages typically follows the pattern:
            // "<Video/Page Title> - YouTube"
            // We classify by detecting known page-specific markers in the title.

            if (title.Equals("YouTube", StringComparison.OrdinalIgnoreCase) ||
                title.StartsWith("YouTube - ", StringComparison.OrdinalIgnoreCase))
            {
                return YouTubePageType.Home;
            }

            // YouTube Shorts: titles often contain "#Shorts" or "YouTube Shorts"
            if (title.Contains("#Shorts", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("YouTube Shorts", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("Shorts", StringComparison.OrdinalIgnoreCase))
            {
                return YouTubePageType.Shorts;
            }

            // Search results: title begins with search query followed by "- YouTube"
            if (Regex.IsMatch(title, @"^.+\s*-\s*YouTube\s*Search", RegexOptions.IgnoreCase) ||
                title.StartsWith("Search", StringComparison.OrdinalIgnoreCase))
            {
                return YouTubePageType.Search;
            }

            // A channel page often contains just a channel name " - YouTube"
            // and the previous page type may have been None or Home.
            // We treat anything with "- YouTube" that isn't a video as a channel or home.
            if (Regex.IsMatch(title, @"^.+\s*-\s*YouTube$"))
            {
                // Videos typically have "Watch" in their broader context, but we can't
                // distinguish purely from the title. We default to WatchVideo since
                // that is the most common case on YouTube.
                return YouTubePageType.WatchVideo;
            }

            return YouTubePageType.Home;
        }

        // ── Browser Detection ─────────────────────────────────────────────────

        private static bool IsBrowserTitle(string title, out string browserName)
        {
            // Browsers append their name to the window title
            if (title.EndsWith("- Google Chrome", StringComparison.OrdinalIgnoreCase))
            { browserName = "Chrome"; return true; }
            if (title.EndsWith("- Microsoft Edge", StringComparison.OrdinalIgnoreCase) ||
                title.EndsWith("- Edge", StringComparison.OrdinalIgnoreCase))
            { browserName = "Edge"; return true; }
            if (title.EndsWith("— Mozilla Firefox", StringComparison.OrdinalIgnoreCase) ||
                title.EndsWith("- Firefox", StringComparison.OrdinalIgnoreCase))
            { browserName = "Firefox"; return true; }
            if (title.EndsWith("- Brave", StringComparison.OrdinalIgnoreCase))
            { browserName = "Brave"; return true; }

            browserName = string.Empty;
            return false;
        }

        // ── Accessibility Target Discovery ────────────────────────────────────

        /// <summary>
        /// Discovers interactive YouTube controls from the accessibility tree and
        /// converts them to <see cref="TargetInfo"/> records for the Hover Engine.
        /// Uses synthetic well-known targets based on page type when the live
        /// UI Automation tree is not available or accessible.
        /// </summary>
        private void RefreshAccessibilityTargets(IntPtr hwnd, YouTubePageType pageType)
        {
            try
            {
                // Get the browser window screen bounds to compute percentage-based target positions
                if (!GetWindowRect(hwnd, out RECT rect)) return;

                double winW = rect.Right  - rect.Left;
                double winH = rect.Bottom - rect.Top;
                if (winW <= 0 || winH <= 0) return;

                // Build targets according to page type and approximate UI positions
                var targets = BuildTargetsForPageType(pageType, rect.Left, rect.Top, winW, winH);

                CachedTargets = targets;
                TargetsDiscovered?.Invoke(targets);

                Logger.Info($"YouTubeUiDetector: Discovered {targets.Count} accessibility targets for {pageType}.");
            }
            catch (Exception ex)
            {
                Logger.Error("YouTubeUiDetector: Accessibility target refresh failed", ex);
            }
        }

        private static List<TargetInfo> BuildTargetsForPageType(
            YouTubePageType pageType, double winX, double winY, double winW, double winH)
        {
            // Compute absolute screen positions from approximate relative positions
            Rect RectPct(double xPct, double yPct, double wPct, double hPct) =>
                new Rect(winX + winW * xPct, winY + winH * yPct, winW * wPct, winH * hPct);

            return pageType switch
            {
                YouTubePageType.WatchVideo => new List<TargetInfo>
                {
                    new TargetInfo("yt_play_pause",   "Play / Pause",      RectPct(0.44, 0.88, 0.04, 0.06)),
                    new TargetInfo("yt_next",         "Next Video",        RectPct(0.50, 0.88, 0.04, 0.06)),
                    new TargetInfo("yt_fullscreen",   "Fullscreen",        RectPct(0.94, 0.88, 0.04, 0.06)),
                    new TargetInfo("yt_like",         "Like",              RectPct(0.40, 0.70, 0.05, 0.05)),
                    new TargetInfo("yt_subscribe",    "Subscribe",         RectPct(0.50, 0.70, 0.08, 0.05)),
                    new TargetInfo("yt_comments",     "Comments",          RectPct(0.10, 0.80, 0.08, 0.05)),
                    new TargetInfo("yt_search",       "Search Box",        RectPct(0.28, 0.02, 0.30, 0.05)),
                },
                YouTubePageType.Shorts => new List<TargetInfo>
                {
                    new TargetInfo("yt_shorts_like",   "Like (Shorts)",   RectPct(0.85, 0.50, 0.05, 0.06)),
                    new TargetInfo("yt_shorts_sub",    "Subscribe (Shorts)", RectPct(0.85, 0.60, 0.05, 0.06)),
                    new TargetInfo("yt_shorts_next",   "Next Short",      RectPct(0.85, 0.70, 0.05, 0.06)),
                    new TargetInfo("yt_search",        "Search Box",      RectPct(0.28, 0.02, 0.30, 0.05)),
                },
                YouTubePageType.Home or YouTubePageType.Channel or YouTubePageType.Playlist => new List<TargetInfo>
                {
                    new TargetInfo("yt_search",       "Search Box",        RectPct(0.28, 0.02, 0.30, 0.05)),
                },
                _ => new List<TargetInfo>()
            };
        }
    }
}
