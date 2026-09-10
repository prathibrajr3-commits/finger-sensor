using System.Collections.Generic;

namespace AirGestureAI.Compatibility
{
    /// <summary>
    /// Provides migration recommendations for plugins or extensions that use deprecated APIs.
    /// </summary>
    public sealed class MigrationAdvisor
    {
        private readonly DeprecationManager _deprecationManager;

        /// <summary>
        /// Initializes a new <see cref="MigrationAdvisor"/>.
        /// </summary>
        public MigrationAdvisor(DeprecationManager deprecationManager)
        {
            _deprecationManager = deprecationManager;
        }

        /// <summary>
        /// Returns a list of migration recommendations for the given list of deprecated API usages.
        /// </summary>
        /// <param name="deprecatedApis">Deprecated API names in use.</param>
        /// <returns>A list of recommendation strings ready to display in the Developer Center.</returns>
        public IReadOnlyList<string> GetRecommendations(IEnumerable<string> deprecatedApis)
        {
            var recommendations = new List<string>();
            foreach (var api in deprecatedApis)
            {
                var replacement = _deprecationManager.GetReplacement(api);
                recommendations.Add(replacement is not null
                    ? $"Replace '{api}' → {replacement}"
                    : $"Remove usage of deprecated API '{api}' (no replacement registered).");
            }
            return recommendations;
        }
    }
}
