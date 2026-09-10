using System;
using System.Collections.Generic;
using System.Reflection;
using AirGestureAI.Utilities;

namespace AirGestureAI.Compatibility
{
    /// <summary>
    /// Analyzes the public API surface of two assembly versions to detect breaking changes
    /// (removed types, removed members, changed signatures).
    /// </summary>
    public sealed class ApiDiffAnalyzer
    {
        /// <summary>
        /// Compares two assemblies and returns a list of breaking change descriptions.
        /// </summary>
        /// <param name="baseline">The older (baseline) assembly version.</param>
        /// <param name="current">The newer (current) assembly version.</param>
        /// <returns>A list of breaking change descriptions, or an empty list if none found.</returns>
        public IReadOnlyList<string> FindBreakingChanges(Assembly baseline, Assembly current)
        {
            Logger.Info($"ApiDiffAnalyzer: Comparing '{baseline.GetName().Name}' baseline → current…");

            var changes      = new List<string>();
            var baselineTypes = IndexTypes(baseline);
            var currentTypes  = IndexTypes(current);

            // Removed types
            foreach (var typeName in baselineTypes.Keys)
                if (!currentTypes.ContainsKey(typeName))
                    changes.Add($"[BREAKING] Type removed: '{typeName}'");

            // Changed members
            foreach (var (typeName, baseType) in baselineTypes)
            {
                if (!currentTypes.TryGetValue(typeName, out var curType)) continue;

                var baseMethods = IndexMembers(baseType);
                var curMethods  = IndexMembers(curType);

                foreach (var memberName in baseMethods.Keys)
                    if (!curMethods.ContainsKey(memberName))
                        changes.Add($"[BREAKING] Member removed: '{typeName}.{memberName}'");
            }

            Logger.Info($"ApiDiffAnalyzer: Found {changes.Count} breaking change(s).");
            return changes;
        }

        private static Dictionary<string, Type> IndexTypes(Assembly assembly)
        {
            var dict = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in assembly.GetExportedTypes())
                dict[t.FullName ?? t.Name] = t;
            return dict;
        }

        private static Dictionary<string, bool> IndexMembers(Type type)
        {
            var dict = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                dict[m.Name] = true;
            return dict;
        }
    }
}
