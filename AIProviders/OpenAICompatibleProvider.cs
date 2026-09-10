using System;
using System.Threading;
using System.Threading.Tasks;

namespace AirGestureAI.AIProviders
{
    /// <summary>
    /// Interfaces with remote or custom OpenAI-compatible REST endpoints.
    /// </summary>
    public class OpenAICompatibleProvider : IAIProvider
    {
        /// <inheritdoc/>
        public string ProviderName => "OpenAI-Compatible";

        /// <inheritdoc/>
        public Task<bool> CheckStatusAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(false); // Default offline simulation
        }

        /// <inheritdoc/>
        public async Task<string> ExecutePromptAsync(string prompt, CancellationToken cancellationToken)
        {
            await Task.Delay(100, cancellationToken);
            throw new InvalidOperationException("Remote OpenAI compatible endpoint unreachable.");
        }
    }
}
