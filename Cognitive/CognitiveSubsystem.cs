using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AirGestureAI.Memory;
using AirGestureAI.Planning;
using AirGestureAI.Semantic;
using AirGestureAI.Utilities;

namespace AirGestureAI.Cognitive
{
    // ── Core Cognitive Models ─────────────────────────────────────────────────

    /// <summary>A timestamped record explaining one cognitive reasoning step.</summary>
    public sealed class ReasoningStep
    {
        /// <summary>Gets the time the step was recorded.</summary>
        public DateTime Timestamp { get; } = DateTime.UtcNow;

        /// <summary>Gets or sets the description of this reasoning step.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Gets or sets the confidence at this step.</summary>
        public double Confidence { get; set; }
    }

    /// <summary>Represents an execution plan built by the Cognitive Engine.</summary>
    public sealed class CognitivePlan
    {
        /// <summary>Gets the raw user goal text.</summary>
        public string Goal { get; init; } = string.Empty;

        /// <summary>Gets the ordered list of action steps.</summary>
        public List<string> Steps { get; init; } = new();

        /// <summary>Gets the reasoning trace for this plan.</summary>
        public List<ReasoningStep> ReasoningTrace { get; } = new();

        /// <summary>Gets or sets the overall execution status.</summary>
        public string Status { get; set; } = "Created";
    }

    // ── Reasoning Engine ──────────────────────────────────────────────────────

    /// <summary>Logs reasoning steps and provides step-by-step explanations.</summary>
    public sealed class ReasoningEngine
    {
        private readonly List<ReasoningStep> _globalTrace = new();

        /// <summary>Gets all recorded global reasoning steps.</summary>
        public IReadOnlyList<ReasoningStep> Trace => _globalTrace;

        /// <summary>Records a reasoning step with a description and confidence.</summary>
        public ReasoningStep Record(string description, double confidence = 1.0)
        {
            var step = new ReasoningStep { Description = description, Confidence = confidence };
            _globalTrace.Add(step);
            Logger.Info($"ReasoningEngine: {description} (conf={confidence:P0})");
            return step;
        }

        /// <summary>Exports the reasoning trace to a text file.</summary>
        public string ExportTrace(string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);
            var path = Path.Combine(outputDirectory, $"reasoning_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            var lines = new List<string>();
            foreach (var step in _globalTrace)
                lines.Add($"[{step.Timestamp:T}] ({step.Confidence:P0}) {step.Description}");
            File.WriteAllLines(path, lines);
            return path;
        }
    }

    // ── Multimodal Fusion ─────────────────────────────────────────────────────

    /// <summary>
    /// Fuses gesture coordinates, voice commands, and active window context
    /// into a unified intent trigger string.
    /// </summary>
    public sealed class MultimodalFusion
    {
        /// <summary>Fuses available inputs into a single intent description.</summary>
        public string Fuse(string? gesture, string? voiceCommand, string? activeApp)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(gesture))     parts.Add($"gesture:{gesture}");
            if (!string.IsNullOrWhiteSpace(voiceCommand)) parts.Add($"voice:{voiceCommand}");
            if (!string.IsNullOrWhiteSpace(activeApp))   parts.Add($"app:{activeApp}");
            return string.Join(" | ", parts);
        }
    }

    // ── Cognitive Logger ──────────────────────────────────────────────────────

    /// <summary>Saves cognitive events and execution histories to files.</summary>
    public sealed class CognitiveLogger
    {
        private readonly string _logPath;

        /// <summary>Initializes a new instance of <see cref="CognitiveLogger"/>.</summary>
        public CognitiveLogger(string logDirectory = "CognitiveLogs")
        {
            Directory.CreateDirectory(logDirectory);
            _logPath = Path.Combine(logDirectory, $"cognitive_{DateTime.Now:yyyyMMdd}.log");
        }

        /// <summary>Appends a cognitive event to the log file.</summary>
        public void Log(string message)
        {
            File.AppendAllText(_logPath, $"[{DateTime.Now:O}] {message}{Environment.NewLine}");
        }
    }

    // ── Central Cognitive Engine ──────────────────────────────────────────────

    /// <summary>
    /// Central orchestrator of the entire cognitive stack. Fuses multimodal inputs,
    /// classifies intent, builds plans, executes them, and logs all reasoning.
    /// </summary>
    public sealed class CognitiveEngine
    {
        private readonly SemanticIntentEngine _semantic;
        private readonly PlanExecutor _planner;
        private readonly MemoryManager _memory;
        private readonly ReasoningEngine _reasoning;
        private readonly MultimodalFusion _fusion;
        private readonly CognitiveLogger _logger;

        /// <summary>Gets the reasoning engine for inspecting past cognitive steps.</summary>
        public ReasoningEngine Reasoning => _reasoning;

        /// <summary>Gets the memory manager for exploring stored associations.</summary>
        public MemoryManager Memory => _memory;

        /// <summary>Initializes a new instance of <see cref="CognitiveEngine"/>.</summary>
        public CognitiveEngine(
            SemanticIntentEngine semantic,
            PlanExecutor planner,
            MemoryManager memory)
        {
            _semantic  = semantic;
            _planner   = planner;
            _memory    = memory;
            _reasoning = new ReasoningEngine();
            _fusion    = new MultimodalFusion();
            _logger    = new CognitiveLogger();
        }

        /// <summary>
        /// Processes a multimodal input, builds a cognitive plan, executes it,
        /// and stores the result in memory.
        /// </summary>
        public async Task<CognitivePlan> ProcessAsync(
            string? gesture        = null,
            string? voiceCommand   = null,
            string? activeApp      = null,
            CancellationToken ct   = default)
        {
            // Step 1: Fuse inputs
            var fusedIntent = _fusion.Fuse(gesture, voiceCommand, activeApp);
            _reasoning.Record($"Fused input: '{fusedIntent}'", 0.95);
            _logger.Log($"Input fused: {fusedIntent}");

            // Step 2: Classify intent
            var intent = await _semantic.ClassifyAsync(fusedIntent, ct);
            _reasoning.Record($"Intent classified: '{intent.Tag}' ({intent.Confidence:P0})", intent.Confidence);

            // Step 3: Build execution plan
            var executionPlan = await _planner.RunAsync(fusedIntent, ct);
            _reasoning.Record($"Plan executed: {executionPlan.Status} ({executionPlan.Tasks.Count} tasks)", 0.90);

            // Step 4: Store outcome in memory
            _memory.Store(intent.Tag, fusedIntent, "cognitive", "auto");
            _memory.Timeline.Add($"Cognitive plan: {intent.Tag}");

            // Build the cognitive plan response
            var plan = new CognitivePlan
            {
                Goal   = voiceCommand ?? gesture ?? "Unknown",
                Status = executionPlan.Status
            };
            foreach (var task in executionPlan.Tasks)
                plan.Steps.Add(task.Action);
            foreach (var step in _reasoning.Trace)
                plan.ReasoningTrace.Add(step);

            _logger.Log($"Plan completed: {plan.Status}");
            return plan;
        }
    }
}
