using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AirGestureAI.Utilities;

namespace AirGestureAI.Services
{
    // ── Session State Models ───────────────────────────────────────────────────

    /// <summary>Represents the persisted window layout state.</summary>
    public sealed class LayoutState
    {
        /// <summary>Gets or sets the main window left position.</summary>
        public double X { get; set; } = 100;

        /// <summary>Gets or sets the main window top position.</summary>
        public double Y { get; set; } = 100;

        /// <summary>Gets or sets the main window width.</summary>
        public double Width { get; set; } = 1280;

        /// <summary>Gets or sets the main window height.</summary>
        public double Height { get; set; } = 720;

        /// <summary>Gets or sets whether the sidebar panel is open.</summary>
        public bool SidebarOpen { get; set; } = true;

        /// <summary>Gets or sets whether the inspector panel is open.</summary>
        public bool InspectorOpen { get; set; } = true;

        /// <summary>Gets or sets the canvas zoom level.</summary>
        public double ZoomLevel { get; set; } = 1.0;

        /// <summary>Gets or sets the dock panel states (panel name → state).</summary>
        public Dictionary<string, string> DockPanels { get; set; } = new();

        /// <summary>Gets or sets the tool window states (name → state).</summary>
        public Dictionary<string, string> ToolWindows { get; set; } = new();
    }

    /// <summary>Represents the persisted lens calibration state.</summary>
    public sealed class CalibrationState
    {
        /// <summary>Gets or sets whether the system has been calibrated.</summary>
        public bool IsCalibrated { get; set; }

        /// <summary>Gets or sets the focal length in pixels.</summary>
        public float FocalLength { get; set; } = 525.0f;

        /// <summary>Gets or sets the horizontal optical center offset.</summary>
        public float PrincipalPointX { get; set; } = 320.0f;

        /// <summary>Gets or sets the vertical optical center offset.</summary>
        public float PrincipalPointY { get; set; } = 240.0f;
    }

    /// <summary>Represents the persisted state of a single visual workflow node.</summary>
    public sealed class WorkflowNodeState
    {
        /// <summary>Gets or sets the node unique identifier.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Gets or sets the display label.</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>Gets or sets the node action parameter.</summary>
        public string Parameter { get; set; } = string.Empty;

        /// <summary>Gets or sets the node type (Action, Delay, Condition, Loop).</summary>
        public string NodeType { get; set; } = "Action";

        /// <summary>Gets or sets the canvas X position.</summary>
        public double CanvasX { get; set; }

        /// <summary>Gets or sets the canvas Y position.</summary>
        public double CanvasY { get; set; }
    }

    /// <summary>Represents the persisted state of a connection between two workflow nodes.</summary>
    public sealed class WorkflowConnectionState
    {
        /// <summary>Gets or sets the source node identifier.</summary>
        public string SourceId { get; set; } = string.Empty;

        /// <summary>Gets or sets the target node identifier.</summary>
        public string TargetId { get; set; } = string.Empty;
    }

    /// <summary>Represents the complete persisted workflow graph state.</summary>
    public sealed class WorkflowState
    {
        /// <summary>Gets or sets all workflow nodes.</summary>
        public List<WorkflowNodeState> Nodes { get; set; } = new();

        /// <summary>Gets or sets all connections between nodes.</summary>
        public List<WorkflowConnectionState> Connections { get; set; } = new();

        /// <summary>Gets or sets named workflow variables.</summary>
        public Dictionary<string, string> Variables { get; set; } = new();

        /// <summary>Gets or sets trigger bindings (trigger name → action).</summary>
        public Dictionary<string, string> Triggers { get; set; } = new();

        /// <summary>
        /// Gets or sets the workflow execution state.
        /// Always restored to Stopped after a crash — never auto-resumed.
        /// </summary>
        public string ExecutionState { get; set; } = "Stopped";
    }

    /// <summary>
    /// Root session state document persisted to disk.
    /// Does NOT contain secrets, keys, or credentials.
    /// </summary>
    public sealed class SessionState
    {
        /// <summary>Gets or sets the schema version string.</summary>
        public string Version { get; set; } = "4.1";

        /// <summary>Gets or sets the timestamp of this session snapshot.</summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>Gets or sets the list of open tab identifiers.</summary>
        public List<string> OpenTabs { get; set; } = new();

        /// <summary>Gets or sets the window layout state.</summary>
        public LayoutState WindowLayout { get; set; } = new();

        /// <summary>Gets or sets the selected camera device index.</summary>
        public int SelectedCamera { get; set; }

        /// <summary>Gets or sets the calibration profile state.</summary>
        public CalibrationState CalibrationProfile { get; set; } = new();

        /// <summary>Gets or sets the list of loaded plugin identifiers.</summary>
        public List<string> LoadedPluginIds { get; set; } = new();

        /// <summary>Gets or sets the workflow graph state.</summary>
        public WorkflowState Workflow { get; set; } = new();

        /// <summary>Gets or sets recent AI assistant messages (non-sensitive content only).</summary>
        public List<string> AiAssistantHistory { get; set; } = new();

        /// <summary>Gets or sets user preferences as key-value pairs.</summary>
        public Dictionary<string, string> UserPreferences { get; set; } = new();

        /// <summary>Gets or sets the active UI theme name.</summary>
        public string Theme { get; set; } = "Dark";

        /// <summary>Gets or sets the active project name.</summary>
        public string ActiveProject { get; set; } = "DefaultProject";
    }

    /// <summary>Wraps the serialized payload with integrity metadata.</summary>
    public sealed class SessionEnvelope
    {
        /// <summary>Gets or sets the schema version.</summary>
        public string Version { get; set; } = "4.1";

        /// <summary>Gets or sets the SHA-256 hex checksum of PayloadJson.</summary>
        public string Checksum { get; set; } = string.Empty;

        /// <summary>Gets or sets the serialized JSON payload string.</summary>
        public string PayloadJson { get; set; } = string.Empty;
    }

    // ── SessionStateManager ───────────────────────────────────────────────────

    /// <summary>
    /// Manages persistence, integrity, backup, checkpoints, and v4.0 migration
    /// for the application's session state. All writes are atomic.
    /// </summary>
    public sealed class SessionStateManager
    {
        private readonly string _appDataPath;
        private readonly string _stateFilePath;
        private readonly string _backupFilePath;
        private readonly string _checkpointsDirectory;
        private readonly LoggingService _logging;

        // Semaphore ensures only one file operation at a time
        private readonly SemaphoreSlim _fileLock = new(1, 1);

        // Safe checkpoint tag: alphanumeric, dash, underscore only — prevents path traversal
        private static readonly Regex SafeTagRegex = new(@"^[a-zA-Z0-9_\-]{1,64}$", RegexOptions.Compiled);

        private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

        /// <summary>Initializes the SessionStateManager.</summary>
        /// <param name="appDataPath">Base application data directory.</param>
        /// <param name="logging">LoggingService for structured event logging.</param>
        public SessionStateManager(string appDataPath, LoggingService logging)
        {
            if (string.IsNullOrWhiteSpace(appDataPath))
                throw new ArgumentException("App data path cannot be null or empty.", nameof(appDataPath));

            _logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _appDataPath = appDataPath;
            _stateFilePath = Path.Combine(appDataPath, "session_state.json");
            _backupFilePath = Path.Combine(appDataPath, "session_state.json.bak");
            _checkpointsDirectory = Path.Combine(appDataPath, "Checkpoints");

            Directory.CreateDirectory(_appDataPath);
            Directory.CreateDirectory(_checkpointsDirectory);
        }

        /// <summary>Gets the path to the primary session state file.</summary>
        public string StateFilePath => _stateFilePath;

        /// <summary>Gets the path to the backup session state file.</summary>
        public string BackupFilePath => _backupFilePath;

        // ── Save ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Atomically saves the provided session state. The previous file is preserved as .bak.
        /// </summary>
        /// <param name="state">The session state to persist.</param>
        /// <param name="targetPath">Optional override target path (used for checkpoints).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task SaveStateAsync(
            SessionState state,
            string? targetPath = null,
            CancellationToken cancellationToken = default)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            var destinationPath = targetPath ?? _stateFilePath;
            var isMainFile = targetPath == null;

            await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            string? tempPath = null;
            try
            {
                state.Timestamp = DateTime.UtcNow;
                var payloadJson = JsonSerializer.Serialize(state, JsonOpts);
                var checksum = ComputeSha256(payloadJson);

                var envelope = new SessionEnvelope
                {
                    Version = state.Version,
                    Checksum = checksum,
                    PayloadJson = payloadJson
                };

                var envelopeJson = JsonSerializer.Serialize(envelope, JsonOpts);

                // Step 1: Write to temp file alongside destination
                tempPath = Path.Combine(
                    Path.GetDirectoryName(destinationPath) ?? _appDataPath,
                    $".tmp_state_{Guid.NewGuid():N}");

                await File.WriteAllTextAsync(tempPath, envelopeJson, Encoding.UTF8, cancellationToken)
                    .ConfigureAwait(false);

                // Step 2: Backup main file before replacement
                if (isMainFile && File.Exists(_stateFilePath))
                {
                    try
                    {
                        File.Copy(_stateFilePath, _backupFilePath, overwrite: true);
                        _logging.Debug("SessionStateManager: Backup updated.", "SessionStateManager");
                    }
                    catch (Exception ex)
                    {
                        _logging.Warning(
                            $"SessionStateManager: Could not update backup: {ex.Message}",
                            "SessionStateManager");
                    }
                }

                // Step 3: Atomic replace (delete + move)
                if (File.Exists(destinationPath))
                    File.Delete(destinationPath);

                File.Move(tempPath, destinationPath);
                tempPath = null; // Successfully moved, clear so finally block doesn't delete it

                _logging.Information(
                    $"SessionStateManager: State saved → '{Path.GetFileName(destinationPath)}' " +
                    $"(v{state.Version}, checksum={checksum[..8]}…).",
                    "SessionStateManager");
            }
            catch (Exception ex)
            {
                _logging.Error(
                    $"SessionStateManager: SaveStateAsync failed for '{destinationPath}'",
                    ex, "SessionStateManager");
                throw;
            }
            finally
            {
                if (tempPath != null)
                {
                    try
                    {
                        if (File.Exists(tempPath))
                            File.Delete(tempPath);
                    }
                    catch { /* Best effort */ }
                }
                _fileLock.Release();
            }
        }

        // ── Restore ───────────────────────────────────────────────────────────

        /// <summary>
        /// Restores the session state following the defined priority order:
        /// 1. Current session file, 2. Backup, 3. Diagnostics autosave, 4. null (default).
        /// </summary>
        public async Task<SessionState?> RestoreStateAsync()
        {
            // 1. Current session file
            if (File.Exists(_stateFilePath))
            {
                var s = await TryLoadAndValidateFileAsync(_stateFilePath).ConfigureAwait(false);
                if (s != null)
                {
                    _logging.Information("SessionStateManager: Restored from primary state file.", "SessionStateManager");
                    return s;
                }
                _logging.Warning("SessionStateManager: Primary state file failed validation.", "SessionStateManager");
            }

            // 2. Backup
            if (File.Exists(_backupFilePath))
            {
                _logging.Warning("SessionStateManager: Attempting restore from backup...", "SessionStateManager");
                var s = await TryLoadAndValidateFileAsync(_backupFilePath).ConfigureAwait(false);
                if (s != null)
                {
                    _logging.Information("SessionStateManager: Restored from backup.", "SessionStateManager");
                    return s;
                }
                _logging.Warning("SessionStateManager: Backup also failed validation.", "SessionStateManager");
            }

            // 3. Diagnostics autosave fallback
            var autosavePath = Path.Combine(
                _appDataPath, "Diagnostics", "session_state_autosave.json");
            if (File.Exists(autosavePath))
            {
                _logging.Warning("SessionStateManager: Attempting restore from diagnostics autosave...", "SessionStateManager");
                var s = await TryLoadAndValidateFileAsync(autosavePath).ConfigureAwait(false);
                if (s != null)
                {
                    _logging.Information("SessionStateManager: Restored from diagnostics autosave.", "SessionStateManager");
                    return s;
                }
            }

            _logging.Warning("SessionStateManager: All restore sources exhausted — returning null (default state).", "SessionStateManager");
            return null;
        }

        // ── Validation ────────────────────────────────────────────────────────

        /// <summary>
        /// Attempts to load and validate a session file. Returns null if validation fails.
        /// Handles: corrupted JSON, missing checksum, checksum mismatch, truncated file,
        /// invalid schema version, and v4.0 → v4.1 migration.
        /// </summary>
        public async Task<SessionState?> TryLoadAndValidateFileAsync(string filePath)
        {
            await _fileLock.WaitAsync().ConfigureAwait(false);
            try
            {
                return await LoadAndValidateInternalAsync(filePath).ConfigureAwait(false);
            }
            finally
            {
                _fileLock.Release();
            }
        }

        private async Task<SessionState?> LoadAndValidateInternalAsync(string filePath)
        {
            if (!File.Exists(filePath)) return null;

            string content;
            try
            {
                content = await File.ReadAllTextAsync(filePath, Encoding.UTF8).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logging.Warning($"SessionStateManager: Cannot read '{filePath}': {ex.Message}", "SessionStateManager");
                return null;
            }

            if (string.IsNullOrWhiteSpace(content) || content.Length < 10)
            {
                _logging.Warning($"SessionStateManager: File '{filePath}' appears truncated.", "SessionStateManager");
                return null;
            }

            SessionEnvelope? envelope;
            try
            {
                envelope = JsonSerializer.Deserialize<SessionEnvelope>(content);
            }
            catch
            {
                _logging.Warning($"SessionStateManager: Corrupted JSON envelope in '{filePath}'.", "SessionStateManager");
                return null;
            }

            if (envelope == null)
            {
                _logging.Warning($"SessionStateManager: Null envelope in '{filePath}'.", "SessionStateManager");
                return null;
            }

            if (string.IsNullOrWhiteSpace(envelope.Checksum))
            {
                _logging.Warning($"SessionStateManager: Missing checksum in '{filePath}'.", "SessionStateManager");
                return null;
            }

            if (string.IsNullOrWhiteSpace(envelope.PayloadJson))
            {
                _logging.Warning($"SessionStateManager: Empty payload in '{filePath}'.", "SessionStateManager");
                return null;
            }

            var computed = ComputeSha256(envelope.PayloadJson);
            if (!string.Equals(computed, envelope.Checksum, StringComparison.OrdinalIgnoreCase))
            {
                _logging.Warning(
                    $"SessionStateManager: Checksum mismatch in '{filePath}' " +
                    $"(expected={envelope.Checksum[..8]}…, got={computed[..8]}…).",
                    "SessionStateManager");
                return null;
            }

            // Version routing
            if (string.Equals(envelope.Version, "4.0", StringComparison.OrdinalIgnoreCase))
            {
                _logging.Information("SessionStateManager: Detected v4.0 session — migrating.", "SessionStateManager");
                return MigrateV40ToV41(envelope.PayloadJson);
            }

            if (!string.Equals(envelope.Version, "4.1", StringComparison.OrdinalIgnoreCase))
            {
                _logging.Warning(
                    $"SessionStateManager: Unknown schema version '{envelope.Version}' in '{filePath}'.",
                    "SessionStateManager");
                return null;
            }

            SessionState? state;
            try
            {
                state = JsonSerializer.Deserialize<SessionState>(envelope.PayloadJson);
            }
            catch (Exception ex)
            {
                _logging.Warning($"SessionStateManager: Failed to deserialize payload: {ex.Message}", "SessionStateManager");
                return null;
            }

            return state;
        }

        // ── v4.0 → v4.1 Migration ─────────────────────────────────────────────

        /// <summary>
        /// Migrates a v4.0 session payload to the v4.1 session model.
        ///
        /// v4.0 findings: The v4.0 workflow is modelled by WorkflowEngine.Workflow with
        /// a flat Steps list (WorkflowStep: Action, Target, Parameters, Status).
        /// The WorkflowEditorViewModel introduced a node-graph model in v4.1.
        /// This migration maps each WorkflowStep to a WorkflowNodeState Action node
        /// and chains them with sequential connections.
        ///
        /// Unknown v4.0 fields are silently ignored to preserve forward compatibility.
        /// </summary>
        private SessionState MigrateV40ToV41(string v40PayloadJson)
        {
            var result = new SessionState { Version = "4.1", Timestamp = DateTime.UtcNow };

            try
            {
                using var doc = JsonDocument.Parse(v40PayloadJson);
                var root = doc.RootElement;

                // Scalar fields
                if (root.TryGetProperty("Theme", out var themeProp))
                    result.Theme = themeProp.GetString() ?? "Dark";

                if (root.TryGetProperty("SelectedCamera", out var camProp))
                    result.SelectedCamera = camProp.TryGetInt32(out var ci) ? ci : 0;

                if (root.TryGetProperty("ActiveProject", out var projProp))
                    result.ActiveProject = projProp.GetString() ?? "DefaultProject";

                // User preferences
                if (root.TryGetProperty("UserPreferences", out var prefsProp)
                    && prefsProp.ValueKind == JsonValueKind.Object)
                {
                    foreach (var p in prefsProp.EnumerateObject())
                        result.UserPreferences[p.Name] = p.Value.GetString() ?? string.Empty;
                }

                // Calibration
                if (root.TryGetProperty("CalibrationProfile", out var calProp)
                    && calProp.ValueKind == JsonValueKind.Object)
                {
                    if (calProp.TryGetProperty("IsCalibrated", out var ic))
                        result.CalibrationProfile.IsCalibrated = ic.GetBoolean();
                    if (calProp.TryGetProperty("FocalLength", out var fl))
                        result.CalibrationProfile.FocalLength = fl.TryGetSingle(out var flv) ? flv : 525f;
                    if (calProp.TryGetProperty("PrincipalPointX", out var ppx))
                        result.CalibrationProfile.PrincipalPointX = ppx.TryGetSingle(out var ppxv) ? ppxv : 320f;
                    if (calProp.TryGetProperty("PrincipalPointY", out var ppy))
                        result.CalibrationProfile.PrincipalPointY = ppy.TryGetSingle(out var ppyv) ? ppyv : 240f;
                }

                // v4.0 Workflow: flat Steps list from WorkflowEngine.Workflow
                // Schema: { Name, Trigger, Steps: [{ Action, Target, Parameters, Status }] }
                JsonElement workflowEl = default;
                bool hasWorkflow =
                    root.TryGetProperty("Workflow", out workflowEl) ||
                    root.TryGetProperty("WorkflowState", out workflowEl);

                if (hasWorkflow && workflowEl.ValueKind == JsonValueKind.Object)
                {
                    if (workflowEl.TryGetProperty("Steps", out var stepsEl)
                        && stepsEl.ValueKind == JsonValueKind.Array)
                    {
                        MigrateV40Steps(stepsEl, result.Workflow);
                    }
                }
                else if (root.TryGetProperty("WorkflowSteps", out var flatSteps)
                    && flatSteps.ValueKind == JsonValueKind.Array)
                {
                    // Some v4.0 snapshots serialised steps at root level
                    MigrateV40Steps(flatSteps, result.Workflow);
                }

                _logging.Information(
                    $"SessionStateManager: v4.0→v4.1 migration complete " +
                    $"({result.Workflow.Nodes.Count} nodes migrated).",
                    "SessionStateManager");
            }
            catch (Exception ex)
            {
                _logging.Error("SessionStateManager: v4.0 migration encountered an error — partial state returned.", ex, "SessionStateManager");
            }

            return result;
        }

        private static void MigrateV40Steps(JsonElement stepsEl, WorkflowState target)
        {
            // Each v4.0 WorkflowStep → v4.1 WorkflowNodeState (Action type)
            // Sequential steps become sequential nodes connected left-to-right.
            string? prevId = null;
            int idx = 0;
            foreach (var step in stepsEl.EnumerateArray())
            {
                var action = step.TryGetProperty("Action", out var a) ? a.GetString() ?? "Action" : "Action";
                var target2 = step.TryGetProperty("Target", out var t) ? t.GetString() ?? string.Empty : string.Empty;
                var parameters = step.TryGetProperty("Parameters", out var p) ? p.GetString() ?? string.Empty : string.Empty;

                var nodeId = $"migrated_node_{idx}";
                target.Nodes.Add(new WorkflowNodeState
                {
                    Id = nodeId,
                    Label = string.IsNullOrWhiteSpace(action) ? $"Step {idx + 1}" : action,
                    Parameter = string.IsNullOrWhiteSpace(parameters) ? target2 : parameters,
                    NodeType = "Action",
                    CanvasX = 60 + idx * 180,
                    CanvasY = 120
                });

                if (prevId != null)
                {
                    target.Connections.Add(new WorkflowConnectionState
                    {
                        SourceId = prevId,
                        TargetId = nodeId
                    });
                }

                prevId = nodeId;
                idx++;
            }

            // Crashed workflow must never auto-resume
            target.ExecutionState = "Stopped";
        }

        // ── Checkpoints ───────────────────────────────────────────────────────

        /// <summary>Creates a named checkpoint of the current session state.</summary>
        /// <param name="tag">Tag name (alphanumeric, dash, underscore; max 64 chars).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task CreateCheckpointAsync(string tag, CancellationToken cancellationToken = default)
        {
            ValidateTag(tag);

            var state = await RestoreStateAsync().ConfigureAwait(false);
            if (state == null)
                throw new InvalidOperationException("Cannot create checkpoint — no valid current state.");

            var path = CheckpointPath(tag);
            await SaveStateAsync(state, path, cancellationToken).ConfigureAwait(false);
            _logging.Information($"SessionStateManager: Checkpoint '{tag}' created.", "SessionStateManager");
        }

        /// <summary>Loads a named checkpoint.</summary>
        public async Task<SessionState?> LoadCheckpointAsync(string tag)
        {
            ValidateTag(tag);
            var path = CheckpointPath(tag);
            if (!File.Exists(path))
            {
                _logging.Warning($"SessionStateManager: Checkpoint '{tag}' not found.", "SessionStateManager");
                return null;
            }

            var state = await TryLoadAndValidateFileAsync(path).ConfigureAwait(false);
            if (state != null)
                _logging.Information($"SessionStateManager: Checkpoint '{tag}' loaded.", "SessionStateManager");
            else
                _logging.Warning($"SessionStateManager: Checkpoint '{tag}' failed validation.", "SessionStateManager");

            return state;
        }

        /// <summary>Lists all available checkpoint tags.</summary>
        public Task<List<string>> ListCheckpointsAsync()
        {
            return Task.Run(() =>
            {
                var tags = new List<string>();
                if (Directory.Exists(_checkpointsDirectory))
                {
                    foreach (var file in Directory.EnumerateFiles(_checkpointsDirectory, "checkpoint_*.json"))
                    {
                        var name = Path.GetFileNameWithoutExtension(file);
                        if (name.StartsWith("checkpoint_", StringComparison.Ordinal))
                            tags.Add(name["checkpoint_".Length..]);
                    }
                }
                return tags;
            });
        }

        /// <summary>Deletes a named checkpoint.</summary>
        public Task DeleteCheckpointAsync(string tag)
        {
            ValidateTag(tag);
            return Task.Run(() =>
            {
                var path = CheckpointPath(tag);
                if (File.Exists(path))
                {
                    File.Delete(path);
                    _logging.Information($"SessionStateManager: Checkpoint '{tag}' deleted.", "SessionStateManager");
                }
            });
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private string CheckpointPath(string tag) =>
            Path.Combine(_checkpointsDirectory, $"checkpoint_{tag}.json");

        private static void ValidateTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                throw new ArgumentException("Checkpoint tag cannot be null or empty.", nameof(tag));
            if (!SafeTagRegex.IsMatch(tag))
                throw new ArgumentException(
                    "Checkpoint tag must contain only alphanumeric characters, dashes, or underscores (max 64).",
                    nameof(tag));
        }

        private static string ComputeSha256(string content)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
