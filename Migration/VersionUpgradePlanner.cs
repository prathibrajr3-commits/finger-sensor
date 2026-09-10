using System;
using System.Collections.Generic;
using AirGestureAI.SDK;

namespace AirGestureAI.Migration
{
    /// <summary>
    /// Plans the sequence of migration steps required to upgrade from one
    /// AirGesture AI version to another.
    /// </summary>
    public sealed class VersionUpgradePlanner
    {
        // ── Step Registry ─────────────────────────────────────────────────────
        // Maps (fromMajor, toMajor) → ordered list of migration step names.
        private static readonly Dictionary<(int From, int To), List<string>> StepMap =
            new()
            {
                // 0.x → 1.x
                { (0, 1), new List<string> { "MigrateConfiguration", "MigratePlugins" } },
                // 1.x → 1.x (patch/minor)
                { (1, 1), new List<string> { "MigrateConfiguration" } },
            };

        /// <summary>
        /// Returns the ordered list of migration step names to execute
        /// when upgrading from <paramref name="fromVersion"/> to <paramref name="toVersion"/>.
        /// </summary>
        public IReadOnlyList<string> Plan(string fromVersion, string toVersion)
        {
            if (!SdkVersion.TryParse(fromVersion, out var from) || from is null)
                throw new ArgumentException($"Cannot parse fromVersion '{fromVersion}'.", nameof(fromVersion));

            if (!SdkVersion.TryParse(toVersion, out var to) || to is null)
                throw new ArgumentException($"Cannot parse toVersion '{toVersion}'.", nameof(toVersion));

            if (StepMap.TryGetValue((from.Major, to.Major), out var steps))
                return steps;

            // No specific migration path — nothing to do
            return Array.Empty<string>();
        }
    }
}
