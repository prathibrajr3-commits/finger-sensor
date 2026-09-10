using System;
using System.Threading;
using System.Threading.Tasks;

namespace AirGestureAI.AIProviders
{
    /// <summary>
    /// Contract implemented by all local and remote AI models.
    /// </summary>
    public interface IAIProvider
    {
        /// <summary>Gets the unique name key of the provider.</summary>
        string ProviderName { get; }

        /// <summary>Checks whether the backend is available offline or online.</summary>
        Task<bool> CheckStatusAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Asynchronously executes text inference using the specified model.
        /// </summary>
        Task<string> ExecutePromptAsync(string prompt, CancellationToken cancellationToken);
    }
}
