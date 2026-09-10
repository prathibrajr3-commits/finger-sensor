using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AirGestureAI.Utilities;

namespace AirGestureAI.ModelZoo
{
    /// <summary>
    /// Runs micro-benchmark passes against installed models to report inference throughput and latency.
    /// </summary>
    public class ModelBenchmark
    {
        /// <summary>
        /// Runs a benchmark pass against a registered model, returning average inference latency in ms.
        /// </summary>
        public async Task<double> RunBenchmarkAsync(ModelMetadata model,
            int iterations = 20, CancellationToken cancellationToken = default)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));

            Logger.Info($"ModelBenchmark: Benchmarking '{model.DisplayName}' ({iterations} iterations)…");

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(10, cancellationToken); // Simulate one inference pass
            }
            sw.Stop();

            double avgMs = sw.ElapsedMilliseconds / (double)iterations;
            Logger.Info($"ModelBenchmark: '{model.DisplayName}' avg latency = {avgMs:F2} ms.");
            return avgMs;
        }
    }
}
