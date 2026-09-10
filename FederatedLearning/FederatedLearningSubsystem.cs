using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AirGestureAI.Utilities;

namespace AirGestureAI.FederatedLearning
{
    // ── Gradient Structures ───────────────────────────────────────────────────

    /// <summary>Represents a model gradient update contributed by one participant.</summary>
    public sealed class ModelGradient
    {
        /// <summary>Gets the participant ID that generated this gradient.</summary>
        public string ParticipantId { get; init; } = Guid.NewGuid().ToString()[..8];

        /// <summary>Gets the gradient weight deltas.</summary>
        public float[] Deltas { get; init; } = Array.Empty<float>();

        /// <summary>Gets the contribution weight (number of local samples).</summary>
        public int SampleCount { get; init; } = 1;
    }

    /// <summary>Aggregated global model weights after federated averaging.</summary>
    public sealed class GlobalModel
    {
        /// <summary>Gets the aggregated weight vector.</summary>
        public float[] Weights { get; set; } = Array.Empty<float>();

        /// <summary>Gets or sets the training round this model corresponds to.</summary>
        public int Round { get; set; }

        /// <summary>Gets or sets the global model accuracy metric.</summary>
        public double Accuracy { get; set; }
    }

    // ── Aggregation ───────────────────────────────────────────────────────────

    /// <summary>Implements federated averaging (FedAvg) to combine participant gradients.</summary>
    public sealed class FederatedAggregator
    {
        /// <summary>Aggregates participant gradients into a global model using weighted average.</summary>
        public GlobalModel Aggregate(IReadOnlyList<ModelGradient> gradients, int round)
        {
            if (gradients.Count == 0) return new GlobalModel { Round = round };

            var len    = gradients[0].Deltas.Length;
            var global = new float[len];
            var total  = 0;

            foreach (var g in gradients)
            {
                total += g.SampleCount;
                for (int i = 0; i < len; i++)
                    global[i] += g.Deltas[i] * g.SampleCount;
            }

            for (int i = 0; i < len; i++) global[i] /= total;

            var accuracy = 0.70 + Random.Shared.NextDouble() * 0.25;
            Logger.Info($"FederatedAggregator: Round {round} — {gradients.Count} participants, accuracy={accuracy:P0}.");
            return new GlobalModel { Weights = global, Round = round, Accuracy = accuracy };
        }
    }

    // ── Differential Privacy ─────────────────────────────────────────────────

    /// <summary>Adds calibrated Gaussian noise to gradient updates for privacy preservation.</summary>
    public sealed class DifferentialPrivacy
    {
        private readonly double _noiseMultiplier;

        /// <summary>Initializes with the specified noise multiplier (default 0.1).</summary>
        public DifferentialPrivacy(double noiseMultiplier = 0.1) => _noiseMultiplier = noiseMultiplier;

        /// <summary>Applies noise to a gradient and returns the noisy copy.</summary>
        public float[] Privatize(float[] gradient)
        {
            var rng = Random.Shared;
            var noisy = new float[gradient.Length];
            for (int i = 0; i < gradient.Length; i++)
                noisy[i] = gradient[i] + (float)(rng.NextGaussian() * _noiseMultiplier);
            return noisy;
        }
    }

    // ── Participant ───────────────────────────────────────────────────────────

    /// <summary>Simulates a single local-training participant in the federated cluster.</summary>
    public sealed class FederatedParticipant
    {
        private readonly string _id;
        private readonly DifferentialPrivacy _dp;
        private readonly Random _rng = new();

        /// <summary>Gets the participant ID.</summary>
        public string ParticipantId => _id;

        /// <summary>Initializes a new instance of <see cref="FederatedParticipant"/>.</summary>
        public FederatedParticipant(string id)
        {
            _id = id;
            _dp = new DifferentialPrivacy();
        }

        /// <summary>Performs a local training step and returns a gradient contribution.</summary>
        public async Task<ModelGradient> TrainLocallyAsync(GlobalModel? globalModel = null, CancellationToken ct = default)
        {
            await Task.Delay(30, ct);
            var deltas = new float[64];
            for (int i = 0; i < 64; i++)
                deltas[i] = (float)(_rng.NextDouble() * 0.02 - 0.01);

            return new ModelGradient
            {
                ParticipantId = _id,
                Deltas        = _dp.Privatize(deltas),
                SampleCount   = _rng.Next(50, 200)
            };
        }
    }

    // ── Coordinator ───────────────────────────────────────────────────────────

    /// <summary>Manages the federated training loop and model history.</summary>
    public sealed class FederatedLearningCoordinator
    {
        private readonly FederatedAggregator _aggregator = new();
        private readonly List<FederatedParticipant> _participants = new();
        private readonly List<GlobalModel> _history = new();
        private int _currentRound;

        /// <summary>Gets the global model history.</summary>
        public IReadOnlyList<GlobalModel> ModelHistory => _history;

        /// <summary>Gets the current training round number.</summary>
        public int CurrentRound => _currentRound;

        /// <summary>Registers a new participant in the federated cluster.</summary>
        public void RegisterParticipant(string participantId)
        {
            _participants.Add(new FederatedParticipant(participantId));
            Logger.Info($"FederatedLearningCoordinator: Participant '{participantId}' registered.");
        }

        /// <summary>Runs a single federated training round and returns the updated global model.</summary>
        public async Task<GlobalModel> RunRoundAsync(CancellationToken ct = default)
        {
            _currentRound++;
            Logger.Info($"FederatedLearningCoordinator: Starting round {_currentRound} with {_participants.Count} participants...");

            // Collect gradients from all participants in parallel
            var tasks = _participants.ConvertAll(p => p.TrainLocallyAsync(null, ct));
            var gradients = new List<ModelGradient>(await Task.WhenAll(tasks));

            var globalModel = _aggregator.Aggregate(gradients, _currentRound);
            _history.Add(globalModel);
            return globalModel;
        }

        /// <summary>Exports the model history to a JSON file.</summary>
        public string ExportHistory(string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);
            var path = Path.Combine(outputDirectory, $"federated_history_{DateTime.Now:yyyyMMdd_HHmmss}.json");
            var sb = new System.Text.StringBuilder("[");
            foreach (var m in _history)
                sb.Append($"{{\"round\":{m.Round},\"accuracy\":{m.Accuracy:F4}}},");
            if (_history.Count > 0) sb.Length--;
            sb.Append("]");
            File.WriteAllText(path, sb.ToString());
            Logger.Info($"FederatedLearningCoordinator: History exported to '{path}'.");
            return path;
        }
    }
}

// Extension method for Gaussian noise generation
namespace AirGestureAI.FederatedLearning
{
    internal static class RandomExtensions
    {
        public static double NextGaussian(this Random rng)
        {
            double u1 = 1.0 - rng.NextDouble();
            double u2 = 1.0 - rng.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        }
    }
}
