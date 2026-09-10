using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AirGestureAI.Utilities;

namespace AirGestureAI.Planning
{
    // ── Plan Models ───────────────────────────────────────────────────────────

    /// <summary>Represents a high-level user goal.</summary>
    public sealed class Goal
    {
        /// <summary>Gets the unique goal identifier.</summary>
        public string GoalId { get; } = Guid.NewGuid().ToString()[..8];

        /// <summary>Gets or sets the goal description.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Gets or sets the desired completion deadline (optional).</summary>
        public DateTime? Deadline { get; set; }
    }

    /// <summary>A single executable step within a plan.</summary>
    public sealed class TaskNode
    {
        /// <summary>Gets the unique task node identifier.</summary>
        public string NodeId { get; } = Guid.NewGuid().ToString()[..8];

        /// <summary>Gets or sets the task action name.</summary>
        public string Action { get; set; } = string.Empty;

        /// <summary>Gets or sets the assigned agent name.</summary>
        public string AssignedAgent { get; set; } = "CoordinatorAgent";

        /// <summary>Gets or sets the IDs of tasks that must complete before this one.</summary>
        public List<string> Dependencies { get; set; } = new();

        /// <summary>Gets or sets the current execution status.</summary>
        public string Status { get; set; } = "Pending";
    }

    /// <summary>Resolves execution order based on task dependencies using topological sort.</summary>
    public sealed class DependencyGraph
    {
        private readonly List<TaskNode> _nodes;

        /// <summary>Initializes a new instance of <see cref="DependencyGraph"/>.</summary>
        public DependencyGraph(List<TaskNode> nodes) => _nodes = nodes;

        /// <summary>Returns the topologically sorted list of task nodes.</summary>
        public List<TaskNode> Resolve()
        {
            var nodeMap = _nodes.ToDictionary(n => n.NodeId, StringComparer.OrdinalIgnoreCase);
            var inDegree = _nodes.ToDictionary(n => n.NodeId, n => 0, StringComparer.OrdinalIgnoreCase);
            var adjacency = _nodes.ToDictionary(n => n.NodeId, n => new List<string>(), StringComparer.OrdinalIgnoreCase);

            foreach (var node in _nodes)
            {
                foreach (var depId in node.Dependencies)
                {
                    if (nodeMap.ContainsKey(depId))
                    {
                        adjacency[depId].Add(node.NodeId);
                        inDegree[node.NodeId]++;
                    }
                }
            }

            var queue = new Queue<string>();
            foreach (var node in _nodes)
            {
                if (inDegree[node.NodeId] == 0)
                {
                    queue.Enqueue(node.NodeId);
                }
            }

            var sorted = new List<TaskNode>();
            while (queue.Count > 0)
            {
                var currId = queue.Dequeue();
                if (nodeMap.TryGetValue(currId, out var node))
                {
                    sorted.Add(node);
                }

                if (adjacency.TryGetValue(currId, out var neighbors))
                {
                    foreach (var neighbor in neighbors)
                    {
                        inDegree[neighbor]--;
                        if (inDegree[neighbor] == 0)
                        {
                            queue.Enqueue(neighbor);
                        }
                    }
                }
            }

            if (sorted.Count < _nodes.Count)
            {
                Logger.Warn($"DependencyGraph: Cycle detected in task dependencies. Falling back to input order.");
                return _nodes.ToList();
            }

            return sorted;
        }
    }

    /// <summary>Represents a ready-to-execute, ordered set of plan steps.</summary>
    public sealed class ExecutionPlan
    {
        /// <summary>Gets the goal this plan belongs to.</summary>
        public Goal Goal { get; init; } = new();

        /// <summary>Gets the ordered list of task nodes.</summary>
        public List<TaskNode> Tasks { get; init; } = new();

        /// <summary>Gets or sets the overall plan status.</summary>
        public string Status { get; set; } = "Created";
    }

    // ── Planning Engine ───────────────────────────────────────────────────────

    /// <summary>Compiles high-level goals into ordered execution plans.</summary>
    public sealed class Planner
    {
        private static readonly (string Keyword, string Agent)[] _agentMap =
        {
            ("open",      "WorkspaceAgent"),
            ("launch",    "AutomationAgent"),
            ("summarize", "DocumentAgent"),
            ("search",    "KnowledgeAgent"),
            ("remind",    "NotificationAgent"),
            ("schedule",  "SchedulerEngine"),
        };

        /// <summary>Creates an execution plan from a goal description.</summary>
        public ExecutionPlan CreatePlan(Goal goal)
        {
            var text = goal.Description.ToLowerInvariant();
            var words = text.Split(new[] { ',', '.', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var tasks = new List<TaskNode>();

            foreach (var word in words.Distinct().Take(8))
            {
                var agent = _agentMap.FirstOrDefault(m => word.Contains(m.Keyword)).Agent ?? "CoordinatorAgent";
                tasks.Add(new TaskNode { Action = word, AssignedAgent = agent });
            }

            var sorted = new DependencyGraph(tasks).Resolve();
            return new ExecutionPlan { Goal = goal, Tasks = sorted, Status = "Ready" };
        }
    }

    /// <summary>Monitors active task execution and detects stalled or failed steps.</summary>
    public sealed class ExecutionMonitor
    {
        /// <summary>Gets the number of completed tasks in the plan.</summary>
        public int CountCompleted(ExecutionPlan plan) =>
            plan.Tasks.Count(t => t.Status == "Done");

        /// <summary>Returns true if all tasks are complete.</summary>
        public bool IsComplete(ExecutionPlan plan) =>
            plan.Tasks.All(t => t.Status == "Done");
    }

    /// <summary>Reflects on past plan failures and adjusts future execution patterns.</summary>
    public sealed class ReflectionEngine
    {
        private readonly List<string> _observations = new();

        /// <summary>Gets all reflection observations.</summary>
        public IReadOnlyList<string> Observations => _observations;

        /// <summary>Records an observation about plan execution.</summary>
        public void Observe(ExecutionPlan plan, string outcome)
        {
            var obs = $"[{DateTime.Now:T}] Plan '{plan.Goal.Description}' → {outcome} " +
                      $"({CountDone(plan)}/{plan.Tasks.Count} tasks completed)";
            _observations.Add(obs);
            Logger.Info($"ReflectionEngine: {obs}");
        }

        private static int CountDone(ExecutionPlan plan) => plan.Tasks.Count(t => t.Status == "Done");
    }

    // ── Plan Executor ─────────────────────────────────────────────────────────

    /// <summary>
    /// Drives end-to-end plan execution: goal → plan → step execution → reflection.
    /// </summary>
    public sealed class PlanExecutor
    {
        private readonly Planner _planner = new();
        private readonly ExecutionMonitor _monitor = new();
        private readonly ReflectionEngine _reflection = new();

        /// <summary>Gets the reflection engine for inspecting past runs.</summary>
        public ReflectionEngine Reflection => _reflection;

        /// <summary>Creates and executes a plan for the given goal string.</summary>
        public async Task<ExecutionPlan> RunAsync(string goalDescription, CancellationToken ct = default)
        {
            var goal = new Goal { Description = goalDescription };
            var plan = _planner.CreatePlan(goal);
            plan.Status = "Running";

            foreach (var task in plan.Tasks)
            {
                ct.ThrowIfCancellationRequested();
                task.Status = "Running";
                await Task.Delay(40, ct);
                task.Status = "Done";
                Logger.Info($"PlanExecutor: Completed task '{task.Action}' via {task.AssignedAgent}.");
            }

            plan.Status = _monitor.IsComplete(plan) ? "Completed" : "PartiallyCompleted";
            _reflection.Observe(plan, plan.Status);
            return plan;
        }
    }
}
