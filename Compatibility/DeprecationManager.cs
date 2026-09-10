using System;
using System.Collections.Generic;

namespace AirGestureAI.Compatibility
{
    /// <summary>
    /// Tracks deprecated API names and produces migration recommendations.
    /// </summary>
    public sealed class DeprecationManager
    {
        private readonly Dictionary<string, string> _deprecations = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Registers a deprecated API and its replacement.
        /// </summary>
        /// <param name="apiName">The deprecated API name.</param>
        /// <param name="replacement">The recommended replacement or migration note.</param>
        public void Register(string apiName, string replacement) =>
            _deprecations[apiName] = replacement;

        /// <summary>
        /// Returns all registered deprecated API names.
        /// </summary>
        public IReadOnlyList<string> GetAllDeprecated() => new List<string>(_deprecations.Keys);

        /// <summary>
        /// Returns the replacement recommendation for a deprecated API, or null if not registered.
        /// </summary>
        public string? GetReplacement(string apiName) =>
            _deprecations.TryGetValue(apiName, out var rep) ? rep : null;

        /// <summary>
        /// Returns whether the given API name is deprecated.
        /// </summary>
        public bool IsDeprecated(string apiName) => _deprecations.ContainsKey(apiName);
    }
}
