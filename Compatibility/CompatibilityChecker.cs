using System;
using System.Collections.Generic;
using System.Reflection;
using AirGestureAI.SDK;
using AirGestureAI.Utilities;

namespace AirGestureAI.Compatibility
{
    /// <summary>
    /// Checks whether a plugin or extension assembly is compatible with the current
    /// SDK version and reports deprecated API usages and breaking changes.
    /// </summary>
    public sealed class CompatibilityChecker
    {
        private readonly ISdkHost _sdkHost;
        private readonly DeprecationManager _deprecationManager;

        /// <summary>
        /// Initializes a new <see cref="CompatibilityChecker"/>.
        /// </summary>
        public CompatibilityChecker(ISdkHost sdkHost)
        {
            _sdkHost            = sdkHost ?? throw new ArgumentNullException(nameof(sdkHost));
            _deprecationManager = new DeprecationManager();
        }

        /// <summary>
        /// Produces a <see cref="CompatibilityReport"/> for the given assembly,
        /// validating its target SDK version and scanning for deprecated API usages.
        /// </summary>
        /// <param name="assembly">The plugin or extension assembly to check.</param>
        /// <param name="requiredSdkVersion">
        /// The minimum SDK version the assembly was built against (from its manifest).
        /// </param>
        public CompatibilityReport Check(Assembly assembly, string requiredSdkVersion)
        {
            Logger.Info($"CompatibilityChecker: Checking '{assembly.GetName().Name}' (requires SDK v{requiredSdkVersion})…");

            var warnings    = new List<string>();
            var errors      = new List<string>();
            var deprecated  = new List<string>();

            // Version check
            if (!_sdkHost.IsCompatible(requiredSdkVersion))
            {
                errors.Add($"Host SDK v{_sdkHost.Version} is not compatible with required v{requiredSdkVersion}.");
            }

            // Scan for [Obsolete] attribute usages on types and members
            foreach (var type in assembly.GetExportedTypes())
            {
                if (type.GetCustomAttribute<ObsoleteAttribute>() is { } typeObs)
                    deprecated.Add($"Type '{type.Name}': {typeObs.Message}");

                foreach (var method in type.GetMethods())
                {
                    if (method.GetCustomAttribute<ObsoleteAttribute>() is { } methObs)
                        deprecated.Add($"Method '{type.Name}.{method.Name}': {methObs.Message}");
                }
            }

            // Registered deprecated APIs
            var registeredDeprecations = _deprecationManager.GetAllDeprecated();
            foreach (var api in registeredDeprecations)
                warnings.Add($"Deprecated API in use: '{api}'");

            var isCompatible = errors.Count == 0;
            Logger.Info($"CompatibilityChecker: '{assembly.GetName().Name}' — {(isCompatible ? "Compatible" : "Incompatible")}");

            return new CompatibilityReport(
                assemblyName: assembly.GetName().Name ?? "Unknown",
                isCompatible: isCompatible,
                errors: errors,
                warnings: warnings,
                deprecatedApis: deprecated);
        }
    }
}
