using System;
using System.Threading;
using System.Threading.Tasks;

namespace AirGestureAI.AIProviders
{
    /// <summary>
    /// Executes predictions locally using lightweight embedded heuristic patterns. Runs entirely offline.
    /// </summary>
    public class LocalModelProvider : IAIProvider
    {
        /// <inheritdoc/>
        public string ProviderName => "Local";

        /// <inheritdoc/>
        public Task<bool> CheckStatusAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(true); // Local is always available
        }

        /// <inheritdoc/>
        public async Task<string> ExecutePromptAsync(string prompt, CancellationToken cancellationToken)
        {
            await Task.Delay(20, cancellationToken); // Lightweight processing delay
            return $"[Local Provider] Simulated response for: \"{prompt}\"";
        }
    }
}
