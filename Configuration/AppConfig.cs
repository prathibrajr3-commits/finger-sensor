namespace AirGestureAI.Configuration
{
    /// <summary>
    /// Holds configuration parameters for the AirGesture AI application.
    /// </summary>
    public class AppConfig
    {
        /// <summary>
        /// Gets or sets the target frames per second for camera capture and hand tracking.
        /// </summary>
        public int TargetFps { get; set; } = 30;

        /// <summary>
        /// Gets or sets the smoothing factor (alpha) for the cursor moving filter.
        /// Lower values increase smoothing but add latency. Value range: (0.0, 1.0].
        /// </summary>
        public double SmoothingFactor { get; set; } = 0.20;

        /// <summary>
        /// Gets or sets the hover selection radius in pixels. If the cursor stays within this radius, it counts as hovering.
        /// </summary>
        public double HoverRadiusPixels { get; set; } = 20.0;

        /// <summary>
        /// Gets or sets the hover dwell selection duration in milliseconds.
        /// </summary>
        public int HoverDurationMs { get; set; } = 1000;

        /// <summary>
        /// Gets or sets the inactivity timeout in milliseconds before the cursor fades out (e.g. 4 seconds).
        /// </summary>
        public int InactivityTimeoutMs { get; set; } = 4000;

        /// <summary>
        /// Gets or sets the stabilization duration in milliseconds for gestures.
        /// </summary>
        public int GestureStabilityMs { get; set; } = 200;

        /// <summary>
        /// Gets or sets the cooldown duration in milliseconds after triggering a gesture.
        /// </summary>
        public int GestureCooldownMs { get; set; } = 500;

        /// <summary>
        /// Gets or sets the scroll velocity threshold (normalized vertical displacement per frame or unit time) for scroll detection.
        /// </summary>
        public double ScrollThresholdY { get; set; } = 0.03;

        /// <summary>
        /// Gets or sets the minimum confidence score (0.0 to 1.0) required for a gesture candidate to be accepted.
        /// </summary>
        public double GestureMinConfidence { get; set; } = 0.50;

        /// <summary>
        /// Gets or sets the minimum motion magnitude (in normalized units/second) required to classify a scroll gesture.
        /// Filters out micro-jitter and accidental tiny movements.
        /// </summary>
        public double GestureMinMotionMagnitude { get; set; } = 0.15;

        /// <summary>
        /// Gets or sets the open palm score threshold (0.0 to 1.0) required to classify an Open Palm gesture.
        /// A value of 0.75 means at least 4 of 5 fingers must be fully extended.
        /// </summary>
        public double OpenPalmScoreThreshold { get; set; } = 0.75;

        /// <summary>
        /// Gets or sets the preferred camera index to open.
        /// </summary>
        public int CameraIndex { get; set; } = 0;

        // ── v4.0.0 Runtime Mode Flags ─────────────────────────────────────────

        /// <summary>
        /// Gets or sets whether the pipeline launches TrackerHost and AIWorker as
        /// isolated subprocesses communicating over Named Pipe IPC.
        /// When false, in-process providers (PythonHandTracker, OpenCvCameraProvider)
        /// are used directly. Default: false (safe in-process mode).
        /// </summary>
        public bool UseIsolatedProcesses { get; set; } = false;

        /// <summary>
        /// Gets or sets whether recognized gestures are routed through the
        /// SemanticIntentEngine and AgentOrchestrator before being dispatched
        /// to raw input simulation.  Default: true.
        /// </summary>
        public bool UseAgentRouting { get; set; } = true;

        /// <summary>
        /// Gets or sets the width of the frame resized for Python MediaPipe tracking.
        /// </summary>
        public int ProcessingWidth { get; set; } = 640;

        /// <summary>
        /// Gets or sets the height of the frame resized for Python MediaPipe tracking.
        /// </summary>
        public int ProcessingHeight { get; set; } = 480;

        // ── Cursor Visual Properties ──────────────────────────────────────────

        /// <summary>
        /// Gets or sets the diameter of the virtual cursor ellipse in pixels.
        /// </summary>
        public double CursorSize { get; set; } = 40.0;

        /// <summary>
        /// Gets or sets the thickness of the cursor border stroke in pixels.
        /// </summary>
        public double CursorStrokeThickness { get; set; } = 2.5;

        /// <summary>
        /// Gets or sets the opacity of the cursor fill (0.0 = fully transparent, 1.0 = fully opaque).
        /// </summary>
        public double CursorFillOpacity { get; set; } = 0.30;

        /// <summary>
        /// Gets or sets the cursor fade-in animation duration in milliseconds.
        /// </summary>
        public int CursorFadeInMs { get; set; } = 180;

        /// <summary>
        /// Gets or sets the cursor fade-out animation duration in milliseconds.
        /// </summary>
        public int CursorFadeOutMs { get; set; } = 300;

        /// <summary>
        /// Gets or sets the minimum pixel displacement required to count as movement (dead zone).
        /// Below this value the cursor position is considered stable (no inactivity reset).
        /// </summary>
        public double MovementDeadZonePixels { get; set; } = 2.0;

        /// <summary>
        /// Gets or sets the trend smoothing factor (beta) used by the Double Exponential Filter.
        /// </summary>
        public double TrendSmoothingFactor { get; set; } = 0.15;

        /// <summary>
        /// Gets or sets the interval in milliseconds at which the cursor engine polls for
        /// inactivity and fires its independent rendering tick.
        /// </summary>
        public int CursorTickIntervalMs { get; set; } = 16; // ~60 Hz poll

        // ── Phase 24: SDK & Extension Ecosystem ───────────────────────────────

        /// <summary>Gets or sets the SDK semantic version string.</summary>
        public string SdkVersion { get; set; } = "4.1.0";

        /// <summary>Gets or sets whether the extension marketplace is enabled.</summary>
        public bool EnableExtensionMarketplace { get; set; } = true;

        /// <summary>Gets or sets whether compatibility mode is enabled for legacy plugins.</summary>
        public bool EnableCompatibilityMode { get; set; } = true;

        /// <summary>Gets or sets whether automatic configuration migration is applied on startup.</summary>
        public bool EnableAutomaticMigration { get; set; } = true;

        /// <summary>Gets or sets whether API documentation generation is enabled.</summary>
        public bool GenerateApiDocumentation { get; set; } = true;

        /// <summary>Gets or sets whether sample project generation is enabled in Developer Center.</summary>
        public bool EnableSampleProjects { get; set; } = true;

        // ── Phase 25: Performance, Security, Accessibility & Release ──────────

        /// <summary>Gets or sets whether performance optimization mode is active.</summary>
        public bool EnablePerformanceMode { get; set; } = true;

        /// <summary>Gets or sets whether power-saving mode is active (reduces CPU usage).</summary>
        public bool EnablePowerSaving { get; set; } = false;

        /// <summary>Gets or sets whether accessibility features are enabled.</summary>
        public bool EnableAccessibility { get; set; } = true;

        /// <summary>Gets or sets the active UI language code (e.g. "en", "ta", "hi").</summary>
        public string Language { get; set; } = "en";

        /// <summary>Gets or sets whether High Contrast UI mode is enabled.</summary>
        public bool EnableHighContrast { get; set; } = false;

        /// <summary>Gets or sets whether the security audit subsystem is enabled.</summary>
        public bool EnableSecurityAudit { get; set; } = true;

        /// <summary>Gets or sets whether offline-only performance telemetry is collected.</summary>
        public bool EnablePerformanceMonitoring { get; set; } = true;

        /// <summary>Gets or sets whether release logging (startup/shutdown events) is enabled.</summary>
        public bool EnableReleaseLogging { get; set; } = true;

        // ── Phase 27: Research, Learning, Analytics, Federated & XR ──────────

        /// <summary>Gets or sets whether adaptive on-device gesture learning is enabled.</summary>
        public bool EnableAdaptiveLearning { get; set; } = true;

        /// <summary>Gets or sets whether federated model synchronization is enabled.</summary>
        public bool EnableFederatedLearning { get; set; } = false;

        /// <summary>Gets or sets whether the core research mode is enabled.</summary>
        public bool EnableResearchMode { get; set; } = false;

        /// <summary>Gets or sets whether analytics collection is enabled.</summary>
        public bool EnableAnalytics { get; set; } = true;

        /// <summary>Gets or sets whether experimental variant evaluations are enabled.</summary>
        public bool EnableExperiments { get; set; } = false;

        /// <summary>Gets or sets whether XR and spatial computing support is enabled.</summary>
        public bool EnableXR { get; set; } = false;

        /// <summary>Gets or sets whether the Model Zoo repository is enabled.</summary>
        public bool EnableModelZoo { get; set; } = true;

        /// <summary>Gets or sets whether external AI providers are permitted.</summary>
        public bool AllowAiProviders { get; set; } = true;

        /// <summary>Gets or sets the default AI provider name.</summary>
        public string DefaultAiProvider { get; set; } = "Local";

        /// <summary>Gets or sets the active AI model name.</summary>
        public string ActiveModel { get; set; } = "Default";

        // ── Phase 29 & 30: Cognitive Engine & Desktop Assistant ──────────

        /// <summary>Gets or sets whether the cognitive engine is enabled.</summary>
        public bool EnableCognitiveEngine { get; set; } = true;

        /// <summary>Gets or sets whether the semantic memory system is enabled.</summary>
        public bool EnableMemorySystem { get; set; } = true;

        /// <summary>Gets or sets whether the workflow studio is enabled.</summary>
        public bool EnableWorkflowStudio { get; set; } = true;

        /// <summary>Gets or sets whether the reasoning engine is enabled.</summary>
        public bool EnableReasoningEngine { get; set; } = true;

        /// <summary>Gets or sets whether multimodal input fusion is enabled.</summary>
        public bool EnableMultimodalFusion { get; set; } = true;

        /// <summary>Gets or sets whether robotics operations are enabled.</summary>
        public bool EnableRobotics { get; set; } = false;

        /// <summary>Gets or sets whether IoT device controls are enabled.</summary>
        public bool EnableIoT { get; set; } = false;

        /// <summary>Gets or sets whether cognitive logging is enabled.</summary>
        public bool EnableCognitiveLogs { get; set; } = true;

        /// <summary>Gets or sets whether intent prediction is enabled.</summary>
        public bool EnableIntentPrediction { get; set; } = true;

        /// <summary>Gets or sets whether the workflow debugger is enabled.</summary>
        public bool EnableWorkflowDebugger { get; set; } = true;

        /// <summary>Gets or sets whether the desktop assistant overlay is enabled.</summary>
        public bool EnableDesktopAssistant { get; set; } = true;

        /// <summary>Gets or sets whether the local knowledge vault index is enabled.</summary>
        public bool EnableKnowledgeVault { get; set; } = true;

        /// <summary>Gets or sets whether goal planning modules are active.</summary>
        public bool EnableGoalPlanning { get; set; } = true;

        /// <summary>Gets or sets whether daily briefing compilation is active.</summary>
        public bool EnableDailyBriefing { get; set; } = true;

        /// <summary>Gets or sets whether desktop process/window automation is enabled.</summary>
        public bool EnableDesktopAutomation { get; set; } = true;

        /// <summary>Gets or sets whether proactive task recommendations are enabled.</summary>
        public bool EnableTaskRecommendations { get; set; } = true;

        /// <summary>Gets or sets whether the local system health monitor is active.</summary>
        public bool EnableSystemHealthMonitor { get; set; } = true;

        /// <summary>Gets or sets whether the smart scheduler engine is active.</summary>
        public bool EnableSmartScheduler { get; set; } = true;

        /// <summary>Gets or sets whether the persistent activity timeline is enabled.</summary>
        public bool EnablePersistentTimeline { get; set; } = true;

        // ── v4.1 Sprint 1 Properties ──────────────────────────────────────────

        /// <summary>Gets or sets whether the setup wizard has been successfully completed.</summary>
        public bool IsFirstRunComplete { get; set; } = false;

        /// <summary>Gets or sets the list of plugin names that have been disabled by the user.</summary>
        public System.Collections.Generic.List<string> DisabledPlugins { get; set; } = new();
    }
}
