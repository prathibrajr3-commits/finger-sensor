using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AirGestureAI.Utilities;

namespace AirGestureAI.Services
{
    /// <summary>Severity levels for recovery validation results.</summary>
    public enum ValidationResultSeverity
    {
        /// <summary>Component is healthy.</summary>
        Healthy,
        /// <summary>Component has a warning but is functional.</summary>
        Warning,
        /// <summary>Component had a recoverable error that was auto-corrected.</summary>
        Recoverable,
        /// <summary>Component is critical — application may be unstable.</summary>
        Critical
    }

    /// <summary>Represents the outcome of validating a single recovered component.</summary>
    public sealed class ComponentValidationResult
    {
        /// <summary>Gets or sets the component name.</summary>
        public string ComponentName { get; set; } = string.Empty;

        /// <summary>Gets or sets the validation severity.</summary>
        public ValidationResultSeverity Severity { get; set; } = ValidationResultSeverity.Healthy;

        /// <summary>Gets or sets the human-readable message.</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>Gets or sets whether the component was successfully repaired.</summary>
        public bool Repaired { get; set; }
    }

    /// <summary>The full session validation report written to validation_report.json.</summary>
    public sealed class SessionValidationReport
    {
        /// <summary>Gets or sets the UTC timestamp of this report.</summary>
        public string Timestamp { get; set; } = DateTime.UtcNow.ToString("O");

        /// <summary>Gets or sets the schema version of the restored session.</summary>
        public string SessionVersion { get; set; } = string.Empty;

        /// <summary>Gets or sets the overall validation severity.</summary>
        public string OverallSeverity { get; set; } = "Healthy";

        /// <summary>Gets or sets the per-component validation results.</summary>
        public List<ComponentValidationResult> ComponentResults { get; set; } = new();

        /// <summary>Gets or sets whether the session was restored with UI notifications suppressed.</summary>
        public bool SilentMode { get; set; }

        /// <summary>Gets or sets whether recovery notification was triggered.</summary>
        public bool NotificationTriggered { get; set; }

        /// <summary>Gets or sets any contextual messages.</summary>
        public List<string> Messages { get; set; } = new();
    }

    /// <summary>
    /// Abstracts the UI notification mechanism to keep RecoveryAgent testable
    /// without a real WPF dispatcher.
    /// </summary>
    public interface IRecoveryNotificationService
    {
        /// <summary>Shows a recovery notification to the user.</summary>
        Task ShowRecoveryNotificationAsync(RecoveryReport report, SessionValidationReport validation);
    }

    /// <summary>
    /// Orchestrates the full session recovery sequence after a crash.
    /// Validates each recovered component, repairs where possible, coordinates
    /// plugin and AI session restoration, emits per-component validation results,
    /// and triggers user-facing notifications.
    /// </summary>
    public sealed class RecoveryAgent
    {
        private readonly SessionStateManager _stateManager;
        private readonly CrashRecoveryService _crashRecovery;
        private readonly LoggingService _logging;
        private readonly IRecoveryNotificationService? _notificationService;
        private readonly string _appDataPath;
        private readonly string _diagnosticsDirectory;

        private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

        /// <summary>Gets the validation report from the last recovery sequence.</summary>
        public SessionValidationReport? LastValidationReport { get; private set; }

        /// <summary>Gets the last restored session state (may be null if none available).</summary>
        public SessionState? RestoredState { get; private set; }

        /// <summary>
        /// Initializes a new <see cref="RecoveryAgent"/>.
        /// </summary>
        /// <param name="stateManager">State persistence manager.</param>
        /// <param name="crashRecovery">Crash detection and lock-file manager.</param>
        /// <param name="logging">Structured logging service.</param>
        /// <param name="appDataPath">Base application data path.</param>
        /// <param name="notificationService">Optional UI notification service.</param>
        public RecoveryAgent(
            SessionStateManager stateManager,
            CrashRecoveryService crashRecovery,
            LoggingService logging,
            string appDataPath,
            IRecoveryNotificationService? notificationService = null)
        {
            _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
            _crashRecovery = crashRecovery ?? throw new ArgumentNullException(nameof(crashRecovery));
            _logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _appDataPath = appDataPath;
            _diagnosticsDirectory = Path.Combine(appDataPath, "Diagnostics");
            _notificationService = notificationService;
            Directory.CreateDirectory(_diagnosticsDirectory);
        }

        // ── Orchestration Entry Point ─────────────────────────────────────────

        /// <summary>
        /// Executes the full recovery sequence:
        /// 1. Runs crash detection + state restoration via CrashRecoveryService.
        /// 2. Validates each recovered component.
        /// 3. Applies repairs and sanitisation.
        /// 4. Triggers the recovery notification if abnormal shutdown was detected.
        /// 5. Writes validation_report.json.
        /// Returns the fully restored and validated SessionState (never null — defaults applied).
        /// </summary>
        public async Task<SessionState> ExecuteRecoveryAsync(
            bool silentMode = false,
            CancellationToken cancellationToken = default)
        {
            _logging.Information("RecoveryAgent: Starting recovery sequence.", "RecoveryAgent");

            // Step 1 — CrashRecovery handles detection + raw restore
            var rawState = await _crashRecovery.RunStartupRecoveryAsync(cancellationToken).ConfigureAwait(false);
            var recoveryReport = _crashRecovery.LastRecoveryReport ?? new RecoveryReport();

            // Step 2 — Use default state if no valid state was recoverable
            if (rawState == null)
            {
                rawState = BuildDefaultState();
                _logging.Information("RecoveryAgent: Using default state (no prior session).", "RecoveryAgent");
            }

            // Step 3 — Validate and repair each component
            var validation = new SessionValidationReport
            {
                Timestamp = DateTime.UtcNow.ToString("O"),
                SessionVersion = rawState.Version,
                SilentMode = silentMode
            };

            ValidateLayout(rawState, validation);
            ValidateCalibration(rawState, validation);
            ValidateWorkflow(rawState, validation);
            ValidatePlugins(rawState, validation);
            ValidateAiHistory(rawState, validation);
            ValidateUserPreferences(rawState, validation);

            // Compute overall severity
            var worst = validation.ComponentResults
                .OrderByDescending(r => (int)r.Severity)
                .FirstOrDefault();
            validation.OverallSeverity = worst?.Severity.ToString() ?? "Healthy";

            // Step 4 — Notify user (unless silent mode or no abnormal shutdown)
            if (!silentMode && _crashRecovery.AbnormalShutdownDetected && _notificationService != null)
            {
                validation.NotificationTriggered = true;
                await _notificationService.ShowRecoveryNotificationAsync(recoveryReport, validation).ConfigureAwait(false);
            }

            // Step 5 — Write validation_report.json
            LastValidationReport = validation;
            await WriteValidationReportAsync(validation).ConfigureAwait(false);

            // Step 6 — Signal startup is stable (resets failure counter)
            await _crashRecovery.MarkStartupStableAsync().ConfigureAwait(false);

            RestoredState = rawState;
            _logging.Information(
                $"RecoveryAgent: Recovery complete. Version={rawState.Version}, " +
                $"Overall={validation.OverallSeverity}.",
                "RecoveryAgent");

            return rawState;
        }

        // ── Component Validators ──────────────────────────────────────────────

        private static void ValidateLayout(SessionState state, SessionValidationReport report)
        {
            const string name = "WindowLayout";

            // Guard against negative or impossible dimensions
            bool repaired = false;
            if (state.WindowLayout.Width < 320)
            {
                state.WindowLayout.Width = 1280;
                repaired = true;
            }
            if (state.WindowLayout.Height < 240)
            {
                state.WindowLayout.Height = 720;
                repaired = true;
            }
            if (state.WindowLayout.X < -20000 || state.WindowLayout.X > 20000)
            {
                state.WindowLayout.X = 100;
                repaired = true;
            }
            if (state.WindowLayout.Y < -20000 || state.WindowLayout.Y > 20000)
            {
                state.WindowLayout.Y = 100;
                repaired = true;
            }

            // Guard against absurd zoom
            if (state.WindowLayout.ZoomLevel < 0.1 || state.WindowLayout.ZoomLevel > 10.0)
            {
                state.WindowLayout.ZoomLevel = 1.0;
                repaired = true;
            }

            if (state.WindowLayout.DockPanels == null)
            {
                state.WindowLayout.DockPanels = new();
                repaired = true;
            }
            if (state.WindowLayout.ToolWindows == null)
            {
                state.WindowLayout.ToolWindows = new();
                repaired = true;
            }

            report.ComponentResults.Add(new ComponentValidationResult
            {
                ComponentName = name,
                Severity = repaired ? ValidationResultSeverity.Recoverable : ValidationResultSeverity.Healthy,
                Message = repaired ? "Layout sanitised — out-of-range values reset to defaults." : "Layout valid.",
                Repaired = repaired
            });
        }

        private static void ValidateCalibration(SessionState state, SessionValidationReport report)
        {
            const string name = "CalibrationProfile";
            bool repaired = false;

            if (state.CalibrationProfile == null)
            {
                state.CalibrationProfile = new CalibrationState();
                repaired = true;
            }

            // Implausible focal length guard
            if (state.CalibrationProfile.FocalLength <= 0 || state.CalibrationProfile.FocalLength > 10_000)
            {
                state.CalibrationProfile.FocalLength = 525.0f;
                state.CalibrationProfile.IsCalibrated = false;
                repaired = true;
            }

            report.ComponentResults.Add(new ComponentValidationResult
            {
                ComponentName = name,
                Severity = repaired ? ValidationResultSeverity.Recoverable : ValidationResultSeverity.Healthy,
                Message = repaired ? "Calibration values repaired — recalibration recommended." : "Calibration valid.",
                Repaired = repaired
            });
        }

        private static void ValidateWorkflow(SessionState state, SessionValidationReport report)
        {
            const string name = "WorkflowGraph";
            bool repaired = false;
            var messages = new List<string>();

            if (state.Workflow == null)
            {
                state.Workflow = new WorkflowState();
                repaired = true;
                messages.Add("Workflow null — reset.");
            }

            if (state.Workflow.Nodes == null)
            {
                state.Workflow.Nodes = new();
                repaired = true;
            }

            if (state.Workflow.Connections == null)
            {
                state.Workflow.Connections = new();
                repaired = true;
            }

            // Validate node IDs are non-empty
            var invalidNodes = state.Workflow.Nodes.Where(n => string.IsNullOrWhiteSpace(n.Id)).ToList();
            foreach (var bad in invalidNodes)
            {
                bad.Id = $"repaired_{Guid.NewGuid():N}";
                repaired = true;
                messages.Add($"Node with empty ID repaired → '{bad.Id}'.");
            }

            // Validate all connection references point to existing node IDs
            var nodeIds = new HashSet<string>(state.Workflow.Nodes.Select(n => n.Id));
            var badConnections = state.Workflow.Connections
                .Where(c => !nodeIds.Contains(c.SourceId) || !nodeIds.Contains(c.TargetId))
                .ToList();
            foreach (var bc in badConnections)
            {
                state.Workflow.Connections.Remove(bc);
                repaired = true;
                messages.Add($"Orphan connection removed: {bc.SourceId}→{bc.TargetId}.");
            }

            // Safety: crashed workflows must never auto-resume
            if (state.Workflow.ExecutionState == "Running")
            {
                state.Workflow.ExecutionState = "Stopped";
                repaired = true;
                messages.Add("Workflow execution state reset from 'Running' to 'Stopped' after crash.");
            }

            if (state.Workflow.Variables == null) state.Workflow.Variables = new();
            if (state.Workflow.Triggers == null) state.Workflow.Triggers = new();

            report.ComponentResults.Add(new ComponentValidationResult
            {
                ComponentName = name,
                Severity = repaired ? ValidationResultSeverity.Recoverable : ValidationResultSeverity.Healthy,
                Message = repaired ? string.Join(" | ", messages) : "Workflow valid.",
                Repaired = repaired
            });
        }

        private static void ValidatePlugins(SessionState state, SessionValidationReport report)
        {
            const string name = "LoadedPlugins";

            if (state.LoadedPluginIds == null)
            {
                state.LoadedPluginIds = new();
                report.ComponentResults.Add(new ComponentValidationResult
                {
                    ComponentName = name,
                    Severity = ValidationResultSeverity.Recoverable,
                    Message = "Plugin list null — reset to empty.",
                    Repaired = true
                });
                return;
            }

            // Remove blanks
            var before = state.LoadedPluginIds.Count;
            state.LoadedPluginIds.RemoveAll(string.IsNullOrWhiteSpace);
            var removed = before - state.LoadedPluginIds.Count;

            report.ComponentResults.Add(new ComponentValidationResult
            {
                ComponentName = name,
                Severity = removed > 0 ? ValidationResultSeverity.Warning : ValidationResultSeverity.Healthy,
                Message = removed > 0 ? $"{removed} blank plugin ID(s) removed." : "Plugin list valid.",
                Repaired = removed > 0
            });
        }

        private static void ValidateAiHistory(SessionState state, SessionValidationReport report)
        {
            const string name = "AiAssistantHistory";

            if (state.AiAssistantHistory == null)
            {
                state.AiAssistantHistory = new();
                report.ComponentResults.Add(new ComponentValidationResult
                {
                    ComponentName = name,
                    Severity = ValidationResultSeverity.Recoverable,
                    Message = "AI history null — reset.",
                    Repaired = true
                });
                return;
            }

            // Cap history to prevent memory inflation
            const int maxHistory = 500;
            bool truncated = false;
            if (state.AiAssistantHistory.Count > maxHistory)
            {
                state.AiAssistantHistory = state.AiAssistantHistory.TakeLast(maxHistory).ToList();
                truncated = true;
            }

            report.ComponentResults.Add(new ComponentValidationResult
            {
                ComponentName = name,
                Severity = truncated ? ValidationResultSeverity.Warning : ValidationResultSeverity.Healthy,
                Message = truncated ? $"AI history truncated to last {maxHistory} entries." : "AI history valid.",
                Repaired = truncated
            });
        }

        private static void ValidateUserPreferences(SessionState state, SessionValidationReport report)
        {
            const string name = "UserPreferences";

            if (state.UserPreferences == null)
            {
                state.UserPreferences = new();
                report.ComponentResults.Add(new ComponentValidationResult
                {
                    ComponentName = name,
                    Severity = ValidationResultSeverity.Recoverable,
                    Message = "User preferences null — reset.",
                    Repaired = true
                });
                return;
            }

            report.ComponentResults.Add(new ComponentValidationResult
            {
                ComponentName = name,
                Severity = ValidationResultSeverity.Healthy,
                Message = $"User preferences valid ({state.UserPreferences.Count} entries)."
            });
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static SessionState BuildDefaultState()
        {
            return new SessionState
            {
                Version = "4.1",
                Timestamp = DateTime.UtcNow,
                Theme = "Dark",
                ActiveProject = "DefaultProject",
                WindowLayout = new LayoutState(),
                CalibrationProfile = new CalibrationState(),
                Workflow = new WorkflowState(),
                LoadedPluginIds = new(),
                AiAssistantHistory = new(),
                UserPreferences = new()
            };
        }

        private async Task WriteValidationReportAsync(SessionValidationReport report)
        {
            try
            {
                var path = Path.Combine(_diagnosticsDirectory, "validation_report.json");
                var json = JsonSerializer.Serialize(report, JsonOpts);
                await File.WriteAllTextAsync(path, json).ConfigureAwait(false);
                _logging.Information("RecoveryAgent: validation_report.json written.", "RecoveryAgent");
            }
            catch (Exception ex)
            {
                _logging.Warning($"RecoveryAgent: Failed to write validation_report.json: {ex.Message}", "RecoveryAgent");
            }
        }
    }
}
