using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AirGestureAI.Utilities;

namespace AirGestureAI.AgentRuntime
{
    // ── Data Models ───────────────────────────────────────────────────────────

    /// <summary>Models a single agentic task node with inputs and outputs.</summary>
    public sealed class AgentTask
    {
        /// <summary>Gets the unique task identifier.</summary>
        public string TaskId { get; } = Guid.NewGuid().ToString()[..8];

        /// <summary>Gets or sets the task name or command.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets or sets the task status.</summary>
        public string Status { get; set; } = "Pending";

        /// <summary>Gets or sets the input payload.</summary>
        public string Input { get; set; } = string.Empty;

        /// <summary>Gets or sets the output result.</summary>
        public string Output { get; set; } = string.Empty;
    }

    /// <summary>Holds an execution report from a completed agentic task.</summary>
    public sealed class AgentResult
    {
        /// <summary>Gets or sets whether the task succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Gets or sets the result payload.</summary>
        public string Result { get; set; } = string.Empty;

        /// <summary>Gets or sets an error message if the task failed.</summary>
        public string? Error { get; set; }
    }

    /// <summary>Tracks active session identifiers and environment variables per agent.</summary>
    public sealed class AgentContext
    {
        /// <summary>Gets the session identifier.</summary>
        public string SessionId { get; } = Guid.NewGuid().ToString();

        /// <summary>Gets the environment variable map.</summary>
        public Dictionary<string, string> Variables { get; } = new();
    }

    /// <summary>Local instance memory cache for an individual agent.</summary>
    public sealed class AgentMemory
    {
        private readonly Dictionary<string, string> _cache = new();

        /// <summary>Stores a value in memory.</summary>
        public void Store(string key, string value) => _cache[key] = value;

        /// <summary>Retrieves a stored value, or null if not found.</summary>
        public string? Retrieve(string key) => _cache.TryGetValue(key, out var v) ? v : null;
    }

    // ── Event Bus ─────────────────────────────────────────────────────────────

    /// <summary>Event broker supporting agent-to-agent topic publishing.</summary>
    public sealed class AgentMessageBus
    {
        private readonly Dictionary<string, List<Action<string>>> _subscribers = new();

        /// <summary>Subscribes a handler to a named topic.</summary>
        public void Subscribe(string topic, Action<string> handler)
        {
            if (!_subscribers.ContainsKey(topic)) _subscribers[topic] = new();
            _subscribers[topic].Add(handler);
        }

        /// <summary>Publishes a message to a named topic.</summary>
        public void Publish(string topic, string payload)
        {
            if (_subscribers.TryGetValue(topic, out var handlers))
                foreach (var h in handlers) h(payload);
        }
    }

    // ── Registry, Scheduler, Lifecycle ────────────────────────────────────────

    /// <summary>Maintains a catalog of registered agent names and their status.</summary>
    public sealed class AgentRegistry
    {
        private readonly Dictionary<string, string> _agents = new();

        /// <summary>Gets all registered agents.</summary>
        public IReadOnlyDictionary<string, string> Agents => _agents;

        /// <summary>Registers an agent with its initial status.</summary>
        public void Register(string agentName, string status = "Idle")
        {
            _agents[agentName] = status;
            Logger.Info($"AgentRegistry: Registered '{agentName}'.");
        }

        /// <summary>Updates the status of a registered agent.</summary>
        public void UpdateStatus(string agentName, string status)
        {
            if (_agents.ContainsKey(agentName)) _agents[agentName] = status;
        }
    }

    /// <summary>Arranges task queues on worker threads with priority support.</summary>
    public sealed class AgentScheduler
    {
        private readonly ConcurrentQueue<AgentTask> _queue = new();

        /// <summary>Gets the number of queued tasks.</summary>
        public int QueueDepth => _queue.Count;

        /// <summary>Enqueues a task.</summary>
        public void Schedule(AgentTask task) => _queue.Enqueue(task);

        /// <summary>Attempts to dequeue the next task.</summary>
        public bool TryDequeue(out AgentTask? task) => _queue.TryDequeue(out task!);
    }

    /// <summary>Handles starting, pausing, resuming, and stopping agents.</summary>
    public sealed class AgentLifecycleManager
    {
        private readonly Dictionary<string, CancellationTokenSource> _tokens = new();

        /// <summary>Starts an agent lifecycle token.</summary>
        public CancellationToken StartAgent(string agentName)
        {
            var cts = new CancellationTokenSource();
            _tokens[agentName] = cts;
            return cts.Token;
        }

        /// <summary>Stops an agent by cancelling its token.</summary>
        public void StopAgent(string agentName)
        {
            if (_tokens.TryGetValue(agentName, out var cts))
            {
                cts.Cancel();
                _tokens.Remove(agentName);
            }
        }
    }

    // ── DAG Planner ───────────────────────────────────────────────────────────

    /// <summary>
    /// Describes a task node in the DAG together with its declared dependencies.
    /// </summary>
    public sealed class DagNode
    {
        /// <summary>Gets the underlying agent task.</summary>
        public AgentTask Task { get; }

        /// <summary>Gets the set of <see cref="AgentTask.TaskId"/> values this node depends on.</summary>
        public HashSet<string> DependsOn { get; } = new(StringComparer.Ordinal);

        /// <summary>Gets or sets the maximum number of retry attempts on failure.</summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>Gets or sets the base delay in milliseconds for exponential back-off.</summary>
        public int BaseDelayMs { get; set; } = 200;

        /// <summary>Initializes a new DAG node wrapping the given task.</summary>
        public DagNode(AgentTask task) => Task = task;
    }

    /// <summary>
    /// Builds and executes a Directed Acyclic Graph of agent tasks.
    /// Tasks within the same dependency tier are run concurrently.
    /// Each task is retried up to <see cref="DagNode.MaxRetries"/> times with
    /// exponential back-off before being marked as failed.
    /// </summary>
    public sealed class DagPlanner
    {
        private readonly Func<AgentTask, CancellationToken, Task<AgentResult>> _executor;

        /// <summary>
        /// Initializes the planner with the function that executes individual tasks.
        /// </summary>
        public DagPlanner(Func<AgentTask, CancellationToken, Task<AgentResult>> executor)
            => _executor = executor;

        /// <summary>
        /// Runs all nodes in topological order, executing each dependency tier concurrently.
        /// Throws <see cref="OperationCanceledException"/> if the token is cancelled.
        /// Throws <see cref="InvalidOperationException"/> if a cycle is detected.
        /// </summary>
        public async Task<IReadOnlyList<AgentResult>> ExecuteAsync(
            IEnumerable<DagNode> nodes,
            CancellationToken ct = default)
        {
            var nodeList = nodes.ToList();

            // Build adjacency and in-degree maps for Kahn's algorithm
            var nodeById   = new Dictionary<string, DagNode>(StringComparer.Ordinal);
            var inDegree   = new Dictionary<string, int>(StringComparer.Ordinal);
            var dependents = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            foreach (var n in nodeList)
            {
                nodeById[n.Task.TaskId]   = n;
                inDegree[n.Task.TaskId]   = n.DependsOn.Count;
                dependents[n.Task.TaskId] = new List<string>();
            }

            foreach (var n in nodeList)
                foreach (var dep in n.DependsOn)
                    if (dependents.TryGetValue(dep, out var list))
                        list.Add(n.Task.TaskId);

            // Kahn's topological sort – process tiers concurrently
            var ready = new Queue<string>();
            foreach (var (id, deg) in inDegree)
                if (deg == 0) ready.Enqueue(id);

            var results   = new List<AgentResult>();
            int processed = 0;

            while (ready.Count > 0)
            {
                ct.ThrowIfCancellationRequested();

                // Drain the current tier
                var tier = new List<string>();
                while (ready.Count > 0) tier.Add(ready.Dequeue());

                // Run all tasks in this tier concurrently
                var tierTasks   = tier.Select(id => ExecuteWithRetryAsync(nodeById[id], ct)).ToArray();
                var tierResults = await Task.WhenAll(tierTasks).ConfigureAwait(false);
                results.AddRange(tierResults);
                processed += tier.Count;

                // Unlock dependents whose all predecessors have completed
                foreach (var id in tier)
                    foreach (var dep in dependents[id])
                    {
                        inDegree[dep]--;
                        if (inDegree[dep] == 0) ready.Enqueue(dep);
                    }
            }

            if (processed != nodeList.Count)
                throw new InvalidOperationException(
                    $"DagPlanner: Cycle detected – only {processed}/{nodeList.Count} tasks could be executed.");

            return results;
        }

        // ── per-task retry with exponential back-off ──────────────────────────

        private async Task<AgentResult> ExecuteWithRetryAsync(DagNode node, CancellationToken ct)
        {
            AgentResult? last = null;

            for (int attempt = 0; attempt <= node.MaxRetries; attempt++)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    node.Task.Status = "Running";
                    var result = await _executor(node.Task, ct).ConfigureAwait(false);

                    if (result.Success)
                    {
                        node.Task.Status = "Done";
                        node.Task.Output = result.Result;
                        Logger.Info(
                            $"DagPlanner: Task '{node.Task.Name}' succeeded on attempt {attempt + 1}.");
                        return result;
                    }

                    last = result;
                    Logger.Error(
                        $"DagPlanner: Task '{node.Task.Name}' failed (attempt {attempt + 1}): {result.Error}");
                }
                catch (OperationCanceledException)
                {
                    node.Task.Status = "Cancelled";
                    throw;
                }
                catch (Exception ex)
                {
                    last = new AgentResult { Success = false, Error = ex.Message };
                    Logger.Error(
                        $"DagPlanner: Task '{node.Task.Name}' threw on attempt {attempt + 1}: {ex.Message}");
                }

                if (attempt < node.MaxRetries)
                {
                    // Exponential back-off: baseDelayMs * 2^attempt, capped at 30 s
                    int delay = Math.Min(node.BaseDelayMs * (1 << attempt), 30_000);
                    Logger.Info($"DagPlanner: Retrying '{node.Task.Name}' in {delay} ms…");
                    await Task.Delay(delay, ct).ConfigureAwait(false);
                }
            }

            node.Task.Status = "Failed";
            return last ?? new AgentResult { Success = false, Error = "Unknown failure." };
        }
    }

    // ── Orchestrator ──────────────────────────────────────────────────────────

    /// <summary>Central orchestrator coordinating the Agent Runtime.</summary>
    public sealed class AgentOrchestrator
    {
        private readonly AgentRegistry         _registry;
        private readonly AgentScheduler        _scheduler;
        private readonly AgentMessageBus       _bus;
        private readonly AgentLifecycleManager _lifecycle;
        private readonly DagPlanner            _dagPlanner;

        /// <summary>Gets the agent registry.</summary>
        public AgentRegistry Registry => _registry;

        /// <summary>Gets the task scheduler.</summary>
        public AgentScheduler Scheduler => _scheduler;

        /// <summary>Gets the message bus.</summary>
        public AgentMessageBus MessageBus => _bus;

        /// <summary>
        /// Raised when a gesture agent task completes, carrying the gesture label
        /// and a semantic explanation produced by intent routing.
        /// </summary>
        public event Action<string, string>? GestureDispatched;

        /// <summary>
        /// Initializes a new instance of <see cref="AgentOrchestrator"/> with
        /// default collaborators. Useful for offline testing and scenarios without DI.
        /// </summary>
        public AgentOrchestrator() : this(new AgentRegistry(), new AgentScheduler(), new AgentMessageBus())
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="AgentOrchestrator"/> with
        /// DI-supplied collaborators so all callers share the same singleton instances.
        /// </summary>
        public AgentOrchestrator(AgentRegistry registry, AgentScheduler scheduler, AgentMessageBus bus)
        {
            _registry  = registry  ?? throw new ArgumentNullException(nameof(registry));
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            _bus       = bus       ?? throw new ArgumentNullException(nameof(bus));
            _lifecycle = new AgentLifecycleManager();

            // Default task executor: yield, publish TaskCompleted, return success.
            // Real sub-agents inject their own Func via a custom DagPlanner.
            _dagPlanner = new DagPlanner(async (task, token) =>
            {
                token.ThrowIfCancellationRequested();
                await Task.Yield();
                _registry.UpdateStatus("CoordinatorAgent", "Running");
                _bus.Publish("TaskCompleted", task.Name);
                Logger.Info($"AgentOrchestrator: Default executor completed task '{task.Name}'.");
                return new AgentResult { Success = true, Result = $"Executed: {task.Name}" };
            });
        }

        /// <summary>Starts the orchestrator and registers all built-in agents.</summary>
        public void Start()
        {
            foreach (var agent in new[] {
                "CoordinatorAgent", "PlanningAgent", "VisionAgent", "GestureAgent",
                "VoiceAgent", "WorkspaceAgent", "AutomationAgent", "DocumentAgent",
                "KnowledgeAgent", "MemoryAgent", "SecurityAgent", "DiagnosticsAgent",
                "NotificationAgent" })
            {
                _registry.Register(agent);
            }

            // Subscribe GestureAgent to the GestureCompleted bus topic
            _bus.Subscribe("GestureCompleted", payload =>
            {
                _registry.UpdateStatus("GestureAgent", "Idle");
                Logger.Info($"AgentOrchestrator: GestureAgent completed gesture '{payload}'.");
            });

            Logger.Info($"AgentOrchestrator: Started with {_registry.Agents.Count} agents registered.");
        }

        /// <summary>
        /// Decomposes a goal string into a linearly-chained DAG and executes it
        /// with per-task retry (max 2 attempts, 100 ms base back-off).
        /// Each punctuation-separated clause becomes one <see cref="DagNode"/>
        /// that depends on the clause before it, preserving sequential intent.
        /// </summary>
        public async Task<List<AgentTask>> SubmitGoalAsync(string goal, CancellationToken ct = default)
        {
            Logger.Info($"AgentOrchestrator: Received goal: '{goal}'");

            var clauses = goal.Split(
                new[] { ',', '.', ';', '!' },
                StringSplitOptions.RemoveEmptyEntries);

            var nodes = new List<DagNode>();
            string? prevId = null;

            foreach (var clause in clauses)
            {
                var task = new AgentTask
                {
                    Name   = clause.Trim(),
                    Input  = clause.Trim(),
                    Status = "Pending"
                };
                var node = new DagNode(task) { MaxRetries = 2, BaseDelayMs = 100 };
                if (prevId is not null) node.DependsOn.Add(prevId);
                nodes.Add(node);
                _scheduler.Schedule(task);
                prevId = task.TaskId;
            }

            try
            {
                await _dagPlanner.ExecuteAsync(nodes, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Logger.Info("AgentOrchestrator: Goal execution cancelled.");
            }
            catch (Exception ex)
            {
                Logger.Error($"AgentOrchestrator: DAG execution error: {ex.Message}");
            }

            return nodes.Select(n => n.Task).ToList();
        }

        /// <summary>
        /// Routes a recognized gesture through the agent pipeline using the DAG planner.
        /// Updates GestureAgent status, publishes to the bus, and raises
        /// <see cref="GestureDispatched"/> for downstream consumers.
        /// </summary>
        public async Task DispatchGestureAsync(string gestureLabel, string semanticExplanation,
            CancellationToken ct = default)
        {
            _registry.UpdateStatus("GestureAgent", "Running");
            Logger.Info($"AgentOrchestrator: Routing gesture '{gestureLabel}' via GestureAgent.");

            var task = new AgentTask
            {
                Name   = $"Gesture:{gestureLabel}",
                Input  = semanticExplanation,
                Status = "Running"
            };
            var node = new DagNode(task) { MaxRetries = 1, BaseDelayMs = 50 };

            try
            {
                await _dagPlanner.ExecuteAsync(new[] { node }, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Logger.Info($"AgentOrchestrator: Gesture dispatch cancelled for '{gestureLabel}'.");
                return;
            }

            _bus.Publish("GestureCompleted", gestureLabel);
            GestureDispatched?.Invoke(gestureLabel, semanticExplanation);

            Logger.Info($"AgentOrchestrator: GestureAgent task '{task.Name}' done.");
        }
    }
}
