using System;
using System.Threading;
using System.Threading.Tasks;

namespace AirGestureAI.AIProviders
{
    /// <summary>
    /// Executes models locally using the ONNX Runtime engine.
    /// </summary>
    public class OnnxProvider : IAIProvider
    {
        /// <inheritdoc/>
        public string ProviderName => "ONNX";

        /// <inheritdoc/>
        public Task<bool> CheckStatusAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(true); // Simulated local availability
        }

        /// <inheritdoc/>
        public async Task<string> ExecutePromptAsync(string prompt, CancellationToken cancellationToken)
        {
            await Task.Delay(50, cancellationToken); // Simulating ONNX GPU/CPU evaluation
            return $"[ONNX Provider] Evaluated: \"{prompt}\"";
        }
    }
}
