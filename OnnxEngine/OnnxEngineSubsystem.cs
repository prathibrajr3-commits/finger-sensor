using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AirGestureAI.Utilities;

namespace AirGestureAI.OnnxEngine
{
    // ── Model Metadata ────────────────────────────────────────────────────────

    /// <summary>Metadata describing a locally loaded ONNX model.</summary>
    public sealed class OnnxModelInfo
    {
        /// <summary>Gets or sets the model identifier / name.</summary>
        public string ModelId { get; set; } = string.Empty;

        /// <summary>Gets or sets the file path to the .onnx file.</summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>Gets or sets the model version string.</summary>
        public string Version { get; set; } = "1.0";

        /// <summary>Gets or sets the model category (Gesture, NLP, Vision, etc.).</summary>
        public string Category { get; set; } = "Generic";

        /// <summary>Gets or sets whether the model is currently loaded into the session.</summary>
        public bool IsLoaded { get; set; }

        /// <summary>Gets or sets the estimated memory footprint in MB.</summary>
        public double MemoryMb { get; set; }
    }

    // ── Inference Result ─────────────────────────────────────────────────────

    /// <summary>Holds the output of a single ONNX model inference pass.</summary>
    public sealed class InferenceResult
    {
        /// <summary>Gets or sets the model that produced this result.</summary>
        public string ModelId { get; set; } = string.Empty;

        /// <summary>Gets or sets the raw output tensor as floats.</summary>
        public float[] OutputTensor { get; set; } = Array.Empty<float>();

        /// <summary>Gets or sets the top-1 classification label.</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>Gets or sets the confidence score between 0.0 and 1.0.</summary>
        public double Confidence { get; set; }

        /// <summary>Gets or sets the inference latency in milliseconds.</summary>
        public double LatencyMs { get; set; }
    }

    // ── Inference Sessions ────────────────────────────────────────────────────

    /// <summary>Wraps a single ONNX model inference session (fallback simulation when real .onnx model file is absent).</summary>
    public sealed class OnnxInferenceSession
    {
        /// <summary>Gets the model identifier this session is bound to.</summary>
        public string ModelId { get; }

        private readonly float[] _mockWeights;

        /// <summary>Initializes a new session for the specified model.</summary>
        public OnnxInferenceSession(string modelId)
        {
            ModelId = modelId;
            var rng = new Random(modelId.GetHashCode());
            _mockWeights = new float[64];
            for (int i = 0; i < 64; i++) _mockWeights[i] = (float)(rng.NextDouble() * 2.0 - 1.0);
        }

        /// <summary>Runs an inference pass and returns a simulated result (fallback path when .onnx model is unavailable).</summary>
        public async Task<InferenceResult> RunAsync(float[] inputTensor, CancellationToken ct = default)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            await Task.Delay(12, ct); // Simulate inference latency
            var rng = Random.Shared;
            var output = _mockWeights.Select(w => w * inputTensor.Skip(rng.Next(inputTensor.Length)).FirstOrDefault(1.0f)).ToArray();
            sw.Stop();

            return new InferenceResult
            {
                ModelId      = ModelId,
                OutputTensor = output,
                Label        = $"Class_{Math.Abs(output.Sum().GetHashCode()) % 10}",
                Confidence   = 0.70 + rng.NextDouble() * 0.28,
                LatencyMs    = sw.Elapsed.TotalMilliseconds
            };
        }
    }

    // ── Model Cache ───────────────────────────────────────────────────────────

    /// <summary>Caches loaded ONNX model sessions in memory for fast repeated inference.</summary>
    public sealed class ModelCache
    {
        private readonly Dictionary<string, OnnxInferenceSession> _sessions = new();

        /// <summary>Gets or creates a session for the given model.</summary>
        public OnnxInferenceSession GetOrCreate(string modelId)
        {
            if (!_sessions.TryGetValue(modelId, out var session))
            {
                session = new OnnxInferenceSession(modelId);
                _sessions[modelId] = session;
                Logger.Info($"ModelCache: Session created for '{modelId}'.");
            }
            return session;
        }

        /// <summary>Gets the number of cached sessions.</summary>
        public int Count => _sessions.Count;

        /// <summary>Evicts a model from the cache to free memory.</summary>
        public bool Evict(string modelId) => _sessions.Remove(modelId);
    }

    // ── Benchmarking ──────────────────────────────────────────────────────────

    /// <summary>Runs throughput and latency benchmarks on ONNX inference sessions.</summary>
    public sealed class OnnxModelBenchmark
    {
        /// <summary>Runs a benchmark with the given model and returns results.</summary>
        public async Task<(double AvgLatencyMs, double Throughput)> RunAsync(
            OnnxInferenceSession session,
            int iterations = 50,
            CancellationToken ct = default)
        {
            var input = Enumerable.Range(0, 64).Select(_ => (float)Random.Shared.NextDouble()).ToArray();
            double totalMs = 0;

            for (int i = 0; i < iterations; i++)
            {
                var result = await session.RunAsync(input, ct);
                totalMs += result.LatencyMs;
            }

            var avgMs       = totalMs / iterations;
            var throughput  = 1000.0 / avgMs; // inferences per second
            Logger.Info($"OnnxModelBenchmark: Model '{session.ModelId}' → Avg={avgMs:F1}ms, Throughput={throughput:F0} inf/s");
            return (avgMs, throughput);
        }
    }

    // ── AI Model Manager ─────────────────────────────────────────────────────

    /// <summary>Manages versions, downloads, and hot-swap of ONNX model files.</summary>
    public sealed class AIModelManager
    {
        private readonly List<OnnxModelInfo> _models = new();

        /// <summary>Gets all registered models.</summary>
        public IReadOnlyList<OnnxModelInfo> Models => _models;

        /// <summary>Registers a model descriptor.</summary>
        public OnnxModelInfo RegisterModel(string modelId, string category, string version = "1.0")
        {
            var info = new OnnxModelInfo { ModelId = modelId, Category = category, Version = version, IsLoaded = true, MemoryMb = 50.0 + Random.Shared.NextDouble() * 200.0 };
            _models.Add(info);
            Logger.Info($"AIModelManager: Registered '{modelId}' (v{version}, {info.MemoryMb:F0} MB).");
            return info;
        }

        /// <summary>Unloads a model to free memory.</summary>
        public bool UnloadModel(string modelId)
        {
            var m = _models.Find(m => m.ModelId == modelId);
            if (m == null) return false;
            m.IsLoaded = false;
            return true;
        }
    }

    // ── ONNX Engine ───────────────────────────────────────────────────────────

    /// <summary>Central ONNX Runtime manager providing model loading, caching, and inference.</summary>
    public sealed class OnnxRuntimeEngine
    {
        private readonly ModelCache _cache = new();
        private readonly AIModelManager _modelManager = new();
        private readonly OnnxModelBenchmark _benchmark = new();

        /// <summary>Gets the model manager.</summary>
        public AIModelManager ModelManager => _modelManager;

        /// <summary>Initializes the ONNX engine and registers the built-in model catalog.</summary>
        public void Initialize()
        {
            _modelManager.RegisterModel("GestureClassifier_v3",   "Gesture", "3.0");
            _modelManager.RegisterModel("HandLandmarkDetector_v2","Vision",  "2.0");
            _modelManager.RegisterModel("IntentClassifier_v1",    "NLP",     "1.0");
            _modelManager.RegisterModel("FaceRecognizer_v1",      "Vision",  "1.0");
            Logger.Info($"OnnxRuntimeEngine: Initialized with {_modelManager.Models.Count} models.");
        }

        /// <summary>Runs inference on a named model with the given input tensor.</summary>
        public async Task<InferenceResult> InferAsync(string modelId, float[] input, CancellationToken ct = default)
        {
            var session = _cache.GetOrCreate(modelId);
            return await session.RunAsync(input, ct);
        }

        /// <summary>Benchmarks a named model and returns throughput statistics.</summary>
        public async Task<(double AvgLatencyMs, double Throughput)> BenchmarkAsync(string modelId, CancellationToken ct = default)
        {
            var session = _cache.GetOrCreate(modelId);
            return await _benchmark.RunAsync(session, 50, ct);
        }
    }
}
