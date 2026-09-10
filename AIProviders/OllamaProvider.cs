using System;
using System.Threading;
using System.Threading.Tasks;

namespace AirGestureAI.AIProviders
{
    /// <summary>
    /// Interfaces with local Ollama HTTP services (e.g. localhost:11434).
    /// </summary>
    public class OllamaProvider : IAIProvider
    {
        /// <inheritdoc/>
        public string ProviderName => "Ollama";

        /// <inheritdoc/>
        public Task<bool> CheckStatusAsync(CancellationToken cancellationToken)
        {
            // Simulated network check to Ollama server
            return Task.FromResult(false); // Default offline simulation
        }

        /// <inheritdoc/>
        public async Task<string> ExecutePromptAsync(string prompt, CancellationToken cancellationToken)
        {
            await Task.Delay(100, cancellationToken);
            throw new InvalidOperationException("Ollama service not running at localhost:11434");
        }
    }
}
