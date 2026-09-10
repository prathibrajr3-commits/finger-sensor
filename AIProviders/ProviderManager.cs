using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AirGestureAI.Utilities;

namespace AirGestureAI.AIProviders
{
    /// <summary>
    /// Manages all registered AI providers, resolves the active backend, and automatically
    /// falls back to <see cref="LocalModelProvider"/> on failure.
    /// </summary>
    public sealed class ProviderManager
    {
        private readonly LocalModelProvider _localFallback = new();
        private readonly Dictionary<string, IAIProvider> _providers;
        private string _activeProviderName;

        /// <summary>Gets the name of the currently active provider.</summary>
        public string ActiveProviderName => _activeProviderName;

        /// <summary>Gets the list of all registered provider names.</summary>
        public IReadOnlyList<string> ProviderNames { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="ProviderManager"/> registering all built-in providers.
        /// </summary>
        public ProviderManager(string defaultProvider = "Local")
        {
            _providers = new Dictionary<string, IAIProvider>(StringComparer.OrdinalIgnoreCase)
            {
                ["Local"]            = _localFallback,
                ["ONNX"]             = new OnnxProvider(),
                ["Ollama"]           = new OllamaProvider(),
                ["LM Studio"]        = new LmStudioProvider(),
                ["OpenAI-Compatible"]= new OpenAICompatibleProvider(),
            };
            ProviderNames = new List<string>(_providers.Keys);
            _activeProviderName = _providers.ContainsKey(defaultProvider) ? defaultProvider : "Local";
        }

        /// <summary>Switches the active provider by name.</summary>
        public bool SetProvider(string name)
        {
            if (_providers.ContainsKey(name))
            {
                _activeProviderName = name;
                Logger.Info($"ProviderManager: Switched to '{name}'.");
                return true;
            }
            Logger.Warn($"ProviderManager: Unknown provider '{name}', keeping '{_activeProviderName}'.");
            return false;
        }

        /// <summary>
        /// Executes a prompt through the active provider, automatically falling back to Local on failure.
        /// </summary>
        public async Task<string> ExecuteAsync(string prompt, CancellationToken cancellationToken = default)
        {
            var provider = _providers[_activeProviderName];
            try
            {
                bool ok = await provider.CheckStatusAsync(cancellationToken);
                if (!ok) throw new InvalidOperationException($"Provider '{_activeProviderName}' reported unavailable.");
                return await provider.ExecutePromptAsync(prompt, cancellationToken);
            }
            catch (Exception ex) when (_activeProviderName != "Local")
            {
                Logger.Warn($"ProviderManager: '{_activeProviderName}' failed ({ex.Message}). Falling back to Local.");
                return await _localFallback.ExecutePromptAsync(prompt, cancellationToken);
            }
        }

        /// <summary>Checks provider availability and returns a status string.</summary>
        public async Task<string> GetProviderStatusAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                bool ok = await _providers[_activeProviderName].CheckStatusAsync(cancellationToken);
                return ok ? "Online" : "Offline";
            }
            catch
            {
                return "Error";
            }
        }
    }
}
