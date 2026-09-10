using System;
using System.Threading;
using System.Threading.Tasks;

namespace AirGestureAI.AIProviders
{
    /// <summary>
    /// Interfaces with local LM Studio HTTP services (e.g. localhost:1234).
    /// </summary>
    public class LmStudioProvider : IAIProvider
    {
        /// <inheritdoc/>
        public string ProviderName => "LM Studio";

        /// <inheritdoc/>
        public Task<bool> CheckStatusAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(false); // Default offline simulation
        }

        /// <inheritdoc/>
        public async Task<string> ExecutePromptAsync(string prompt, CancellationToken cancellationToken)
        {
            await Task.Delay(100, cancellationToken);
            throw new InvalidOperationException("LM Studio service not running at localhost:1234");
        }
    }
}
