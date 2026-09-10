using System;
using System.Collections.Generic;

namespace AirGestureAI.Extensions
{
    /// <summary>
    /// Resolves extension dependency graphs and detects unresolved dependencies.
    /// </summary>
    public sealed class ExtensionDependencyResolver
    {
        /// <summary>
        /// Given a list of required dependency IDs and currently installed extension IDs,
        /// returns which dependencies are not satisfied.
        /// </summary>
        /// <param name="required">Required dependency extension IDs.</param>
        /// <param name="installed">Currently installed extension IDs.</param>
        /// <returns>A list of unresolved dependency IDs.</returns>
        public IReadOnlyList<string> Resolve(
            IEnumerable<string> required,
            IEnumerable<string> installed)
        {
            var installedSet = new HashSet<string>(installed, StringComparer.OrdinalIgnoreCase);
            var unresolved   = new List<string>();

            foreach (var dep in required)
                if (!installedSet.Contains(dep))
                    unresolved.Add(dep);

            return unresolved;
        }
    }
}
