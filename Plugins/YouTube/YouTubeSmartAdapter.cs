using System;
using System.Collections.Generic;
using AirGestureAI.HoverSelection;
using AirGestureAI.Input;
using AirGestureAI.Models;
using AirGestureAI.Plugins;
using AirGestureAI.Utilities;

namespace AirGestureAI.Plugins.YouTube
{
    /// <summary>
    /// YouTube Smart Adapter — Phase 11.
    /// Implements <see cref="IApplicationAdapter"/> to provide context-aware gesture
    /// control for YouTube running in Chrome, Edge, Brave, or Firefox.
    ///
    /// <para>
    /// This adapter uses <see cref="YouTubeUiDetector"/> to identify the current
    /// YouTube page type and accessible interactive targets.  Gesture routing is
    /// then mapped according to current page context:
    /// <list type="bullet">
    ///   <item><description>Open Palm → Play/Pause (video pages)</description></item>
    ///   <item><description>Scroll Up → Previous Short / Page-scroll up</description></item>
    ///   <item><description>Scroll Down → Next Short / Page-scroll down</description></item>
    ///   <item><description>Hover Select → Activate hovered accessibility target</description></item>
    /// </list>
    /// </para>
    /// </summary>
    public sealed class YouTubeSmartAdapter : IApplicationAdapter
    {
        // ── Metadata ──────────────────────────────────────────────────────────
        public PluginDescriptor Descriptor { get; } = new PluginDescriptor
        {
            Name        = "YouTube Smart Adapter",
            Version     = "1.1.0",
            Author      = "AirGesture AI",
            Description = "Context-aware gesture control for YouTube — supports video, Shorts, home, search, and playlist pages.",
            MinCoreVersion = "1.1.0",
            SupportedApplications = new List<ApplicationType>
            {
                ApplicationType.Chrome,
                ApplicationType.Edge,
                ApplicationType.Firefox,
                ApplicationType.Brave
            }
        };

        // ── Dependencies ──────────────────────────────────────────────────────
        private readonly IHoverEngine _hoverEngine;
        private readonly IInputSimulator _inputSimulator;
        private readonly YouTubeUiDetector _detector;

        // ── State ─────────────────────────────────────────────────────────────
        private bool _isRunning;
        private ApplicationContext? _currentContext;

        // Diagnostics exposure
        public YouTubePageType CurrentPageType => _detector.CurrentPageType;
        public bool IsYouTubeActive => _detector.IsYouTubeActive;
        public string ActiveBrowser => _detector.ActiveBrowser;
        public int CachedTargetCount => _detector.CachedTargets.Count;
        public DateTime LastUpdate => _detector.LastUpdate;

        /// <summary>
        /// Initializes the adapter with the core hover and input engine dependencies.
        /// </summary>
        public YouTubeSmartAdapter(IHoverEngine hoverEngine, IInputSimulator inputSimulator)
        {
            _hoverEngine    = hoverEngine    ?? throw new ArgumentNullException(nameof(hoverEngine));
            _inputSimulator = inputSimulator ?? throw new ArgumentNullException(nameof(inputSimulator));
            _detector       = new YouTubeUiDetector();
        }

        // ── IPlugin Lifecycle ─────────────────────────────────────────────────

        public bool Initialize()
        {
            Logger.Info("YouTubeSmartAdapter: Initializing...");

            _detector.PageTypeChanged   += OnPageTypeChanged;
            _detector.TargetsDiscovered += OnTargetsDiscovered;

            Logger.Info("YouTubeSmartAdapter: Initialized successfully.");
            return true;
        }

        public void Start()
        {
            if (_isRunning) return;
            _isRunning = true;
            _detector.Start();
            Logger.Info("YouTubeSmartAdapter: Started.");
        }

        public void Stop()
        {
            if (!_isRunning) return;
            _isRunning = false;
            _detector.Stop();
            _hoverEngine.ClearTargets();
            Logger.Info("YouTubeSmartAdapter: Stopped.");
        }

        public void Dispose()
        {
            Stop();
            _detector.PageTypeChanged   -= OnPageTypeChanged;
            _detector.TargetsDiscovered -= OnTargetsDiscovered;
            _detector.Dispose();
        }

        // ── IApplicationAdapter ───────────────────────────────────────────────

        public void OnApplicationChanged(ApplicationContext context)
        {
            _currentContext = context;
            Logger.Info($"YouTubeSmartAdapter: Application context changed to {context.AppType} — \"{context.WindowTitle}\"");

            if (!_detector.IsYouTubeActive)
            {
                // The UI Automation detector will determine page status via title
                // on its next poll tick. Clear existing targets until re-confirmed.
                _hoverEngine.ClearTargets();
            }
        }

        public string? ProcessGesture(GestureType gesture, ApplicationContext context, out bool overrideDefault)
        {
            overrideDefault = false;

            if (!_detector.IsYouTubeActive) return null;

            var page = _detector.CurrentPageType;

            switch (gesture)
            {
                case GestureType.OpenPalm when page == YouTubePageType.WatchVideo:
                    Logger.Info("YouTubeSmartAdapter: Open Palm → Play/Pause (video page)");
                    _inputSimulator.PressSpace();
                    overrideDefault = true;
                    return "Play/Pause video";

                case GestureType.ScrollUp when page == YouTubePageType.Shorts:
                    Logger.Info("YouTubeSmartAdapter: Scroll Up → Previous Short");
                    // YouTube Shorts responds to arrow key Up for previous
                    SimulateArrowKey(isUp: true);
                    overrideDefault = true;
                    return "Previous Short";

                case GestureType.ScrollDown when page == YouTubePageType.Shorts:
                    Logger.Info("YouTubeSmartAdapter: Scroll Down → Next Short");
                    SimulateArrowKey(isUp: false);
                    overrideDefault = true;
                    return "Next Short";

                case GestureType.ScrollUp:
                    // Non-Shorts pages: pass through to default scroll up action
                    Logger.Info($"YouTubeSmartAdapter: Scroll Up on {page} — delegating to default input.");
                    _inputSimulator.ScrollUp();
                    overrideDefault = true;
                    return "Scroll page up";

                case GestureType.ScrollDown:
                    Logger.Info($"YouTubeSmartAdapter: Scroll Down on {page} — delegating to default input.");
                    _inputSimulator.ScrollDown();
                    overrideDefault = true;
                    return "Scroll page down";

                default:
                    return null; // Let default behavior handle
            }
        }

        // ── Detector Event Handlers ────────────────────────────────────────────

        private void OnPageTypeChanged(YouTubePageType pageType)
        {
            Logger.Info($"YouTubeSmartAdapter: Page changed to {pageType}. Clearing hover targets for refresh.");
            _hoverEngine.ClearTargets();
        }

        private void OnTargetsDiscovered(IReadOnlyList<TargetInfo> targets)
        {
            Logger.Info($"YouTubeSmartAdapter: Registering {targets.Count} hover targets.");
            _hoverEngine.ClearTargets();
            _hoverEngine.RegisterTargets(targets);
        }

        // ── Input Helpers ─────────────────────────────────────────────────────

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT_STRUCT[] pInputs, int cbSize);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct INPUT_STRUCT
        {
            public uint type;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValArray, SizeConst = 28)]
            public byte[] Data;
        }

        private static void SimulateArrowKey(bool isUp)
        {
            // Use simple SendInput for keyboard arrow Up (0x26) or Down (0x28)
            const int INPUT_KEYBOARD = 1;
            const uint KEYEVENTF_KEYUP = 0x0002;
            ushort vk = isUp ? (ushort)0x26 : (ushort)0x28; // VK_UP / VK_DOWN

            try
            {
                // Build minimal INPUT struct via byte array to avoid full struct definition
                // We inline the KEYBDINPUT bytes: wVk(2), wScan(2), dwFlags(4), time(4), dwExtraInfo(8) = 20 bytes
                var inputs = new INPUT_STRUCT[2];

                // Key Down
                inputs[0] = new INPUT_STRUCT { type = INPUT_KEYBOARD, Data = new byte[28] };
                Buffer.BlockCopy(BitConverter.GetBytes(vk), 0, inputs[0].Data, 0, 2);

                // Key Up
                inputs[1] = new INPUT_STRUCT { type = INPUT_KEYBOARD, Data = new byte[28] };
                Buffer.BlockCopy(BitConverter.GetBytes(vk), 0, inputs[1].Data, 0, 2);
                Buffer.BlockCopy(BitConverter.GetBytes(KEYEVENTF_KEYUP), 0, inputs[1].Data, 4, 4);

                SendInput(2, inputs, System.Runtime.InteropServices.Marshal.SizeOf(typeof(INPUT_STRUCT)));
            }
            catch (Exception ex)
            {
                Logger.Error("YouTubeSmartAdapter: SendInput arrow key failed", ex);
            }
        }
    }
}
