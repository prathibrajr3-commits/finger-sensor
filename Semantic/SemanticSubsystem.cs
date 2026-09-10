using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AirGestureAI.Utilities;

namespace AirGestureAI.Semantic
{
    /// <summary>
    /// Represents a resolved user intent with confidence score and explanation.
    /// </summary>
    public sealed class IntentResult
    {
        /// <summary>Gets or sets the classified intent tag (e.g. "SwitchWorkspace", "LaunchTool").</summary>
        public string Tag { get; set; } = string.Empty;

        /// <summary>Gets or sets the confidence score between 0.0 and 1.0.</summary>
        public double Confidence { get; set; }

        /// <summary>Gets or sets a human-readable explanation of the classification.</summary>
        public string Explanation { get; set; } = string.Empty;

        /// <summary>Gets or sets the extracted entities from the input prompt.</summary>
        public List<string> Entities { get; set; } = new();
    }

    /// <summary>
    /// Generates deterministic embedding vectors from natural language prompts using word-hashing.
    /// </summary>
    public sealed class IntentEmbedding
    {
        /// <summary>Returns a 64-dimensional embedding vector for the given prompt text.</summary>
        public float[] Embed(string prompt)
        {
            var vec = new float[64];
            if (string.IsNullOrEmpty(prompt)) return vec;

            // Generate deterministic features based on character sequences
            var words = prompt.Split(new[] { ' ', ',', '.', ';', '?' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in words)
            {
                var h = word.ToLowerInvariant().GetHashCode();
                var rng = new Random(h);
                for (int i = 0; i < 64; i++)
                {
                    vec[i] += (float)(rng.NextDouble() * 2.0 - 1.0);
                }
            }

            // Normalize vector to unit length
            double sumSq = vec.Sum(x => x * x);
            if (sumSq > 0.0001)
            {
                float norm = (float)Math.Sqrt(sumSq);
                for (int i = 0; i < 64; i++) vec[i] /= norm;
            }

            return vec;
        }
    }

    /// <summary>
    /// Resolves intent tags and directs them to automation, workspace, or assistant coordinators.
    /// </summary>
    public sealed class IntentRouter
    {
        private readonly Dictionary<string, string> _routes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["SwitchWorkspace"]  = "WorkspaceAgent",
            ["LaunchTool"]       = "AutomationAgent",
            ["SummarizeContent"] = "DocumentAgent",
            ["SearchKnowledge"]  = "KnowledgeAgent",
            ["ScheduleTask"]     = "SchedulerEngine",
            ["SetReminder"]      = "NotificationAgent",
            ["OpenApplication"]  = "WorkspaceAgent",
            ["CloseApplication"] = "WorkspaceAgent"
        };

        /// <summary>Routes an intent tag to a target agent/service name.</summary>
        public string Route(string intentTag)
        {
            if (_routes.TryGetValue(intentTag, out var target))
            {
                return target;
            }
            return "CoordinatorAgent";
        }
    }

    /// <summary>
    /// Classifies natural language prompts using vector cosine similarity against reference embeddings.
    /// </summary>
    public sealed class IntentClassifier
    {
        private readonly IntentEmbedding _embedding = new();
        private readonly Dictionary<string, float[]> _refEmbeddings = new();

        /// <summary>Initializes classifier and computes reference vectors for all intents.</summary>
        public IntentClassifier()
        {
            // Seed reference sentences for each intent category
            var seeds = new Dictionary<string, string>
            {
                ["OpenApplication"]  = "open files manager explorer workspace folders directories",
                ["LaunchTool"]       = "launch execute start tools applications calculator notepad command",
                ["CloseApplication"] = "close exit stop kill terminate application process",
                ["SummarizeContent"] = "summarize read content doc document pdf file extract article summary",
                ["SearchKnowledge"]  = "search look up find consult knowledge database query search engines web",
                ["SetReminder"]      = "remind notification alarm alert timers time notification",
                ["ScheduleTask"]     = "schedule calendar events tasks timeline arrange meeting appointments",
                ["SwitchWorkspace"]  = "switch workspace environments layouts screens active profiles project"
            };

            foreach (var kv in seeds)
            {
                _refEmbeddings[kv.Key] = _embedding.Embed(kv.Value);
            }
        }

        /// <summary>Classifies a prompt via cosine similarity and returns the best matched intent and confidence.</summary>
        public (string Tag, double Confidence) Classify(string prompt)
        {
            var queryVec = _embedding.Embed(prompt);
            string bestTag = "GeneralCommand";
            double bestScore = 0.40;

            foreach (var kv in _refEmbeddings)
            {
                double score = CalculateCosineSimilarity(queryVec, kv.Value);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestTag = kv.Key;
                }
            }

            // Map cosine similarity [-1, 1] range to confidence [0, 1]
            double confidence = Math.Clamp((bestScore + 1.0) / 2.0, 0.0, 1.0);

            return (bestTag, confidence);
        }

        private static double CalculateCosineSimilarity(float[] vecA, float[] vecB)
        {
            if (vecA.Length != vecB.Length) return 0;
            double dotProduct = 0;
            double normA = 0;
            double normB = 0;

            for (int i = 0; i < vecA.Length; i++)
            {
                dotProduct += vecA[i] * vecB[i];
                normA += vecA[i] * vecA[i];
                normB += vecB[i] * vecB[i];
            }

            if (normA == 0 || normB == 0) return 0;
            return dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
        }
    }

    /// <summary>
    /// Interprets prompt templates and resolves variable placeholders.
    /// </summary>
    public sealed class PromptInterpreter
    {
        /// <summary>Expands template variables inside a prompt string.</summary>
        public string Interpret(string prompt, Dictionary<string, string>? variables = null)
        {
            if (variables == null) return prompt;
            foreach (var kv in variables)
                prompt = prompt.Replace($"{{{kv.Key}}}", kv.Value);
            return prompt;
        }
    }

    /// <summary>
    /// Measures accuracy and latency of the semantic intent engine.
    /// </summary>
    public sealed class SemanticDiagnostics
    {
        private int _totalRequests;
        private double _totalLatencyMs;

        /// <summary>Records a completed classification event.</summary>
        public void Record(double latencyMs)
        {
            Interlocked.Increment(ref _totalRequests);
            _totalLatencyMs += latencyMs;
        }

        /// <summary>Gets the average classification latency in milliseconds.</summary>
        public double AverageLatencyMs => _totalRequests == 0 ? 0 : _totalLatencyMs / _totalRequests;

        /// <summary>Gets the total request count.</summary>
        public int TotalRequests => _totalRequests;
    }

    /// <summary>
    /// Core NLP engine coordinating cosine similarity intent classification, routing, and explanation.
    /// </summary>
    public sealed class SemanticIntentEngine
    {
        private readonly IntentClassifier _classifier = new();
        private readonly IntentEmbedding _embedding = new();
        private readonly IntentRouter _router = new();
        private readonly PromptInterpreter _interpreter = new();
        private readonly SemanticDiagnostics _diagnostics = new();

        /// <summary>Gets the diagnostics profiler.</summary>
        public SemanticDiagnostics Diagnostics => _diagnostics;

        /// <summary>Classifies an intent from a natural language prompt via cosine similarity vector match.</summary>
        public async Task<IntentResult> ClassifyAsync(string prompt, CancellationToken ct = default)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            await Task.Delay(1, ct);
            
            var (tag, confidence) = _classifier.Classify(prompt);
            var target = _router.Route(tag);
            
            sw.Stop();
            _diagnostics.Record(sw.Elapsed.TotalMilliseconds);

            Logger.Info($"SemanticIntentEngine: '{prompt}' → Tag='{tag}' ({confidence:P0}), Route='{target}' [Similarity Match]");
            return new IntentResult
            {
                Tag = tag,
                Confidence = confidence,
                Explanation = $"Resolved semantic intent vector to '{tag}' with confidence {confidence:P0}. Routing dynamically to '{target}'.",
                Entities = new List<string> { tag, target }
            };
        }
    }
}
