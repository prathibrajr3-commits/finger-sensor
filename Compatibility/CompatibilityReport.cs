using System;
using System.Collections.Generic;

namespace AirGestureAI.Compatibility
{
    /// <summary>
    /// Immutable report produced by the <see cref="CompatibilityChecker"/>
    /// after inspecting a plugin or extension assembly.
    /// </summary>
    public sealed class CompatibilityReport
    {
        /// <summary>Gets the name of the inspected assembly.</summary>
        public string AssemblyName { get; }

        /// <summary>Gets whether the assembly is compatible with the current SDK.</summary>
        public bool IsCompatible { get; }

        /// <summary>Gets the list of errors preventing compatibility.</summary>
        public IReadOnlyList<string> Errors { get; }

        /// <summary>Gets the list of non-fatal warnings.</summary>
        public IReadOnlyList<string> Warnings { get; }

        /// <summary>Gets the list of deprecated API usages detected.</summary>
        public IReadOnlyList<string> DeprecatedApis { get; }

        /// <summary>Gets the UTC time when this report was generated.</summary>
        public DateTime GeneratedAtUtc { get; } = DateTime.UtcNow;

        /// <summary>
        /// Initializes a new <see cref="CompatibilityReport"/>.
        /// </summary>
        public CompatibilityReport(
            string assemblyName,
            bool isCompatible,
            IReadOnlyList<string> errors,
            IReadOnlyList<string> warnings,
            IReadOnlyList<string> deprecatedApis)
        {
            AssemblyName   = assemblyName   ?? "Unknown";
            IsCompatible   = isCompatible;
            Errors         = errors         ?? Array.Empty<string>();
            Warnings       = warnings       ?? Array.Empty<string>();
            DeprecatedApis = deprecatedApis ?? Array.Empty<string>();
        }

        /// <summary>Returns a human-readable summary.</summary>
        public override string ToString() =>
            $"{AssemblyName}: {(IsCompatible ? "Compatible" : "Incompatible")} " +
            $"| Errors: {Errors.Count} | Warnings: {Warnings.Count} | Deprecated APIs: {DeprecatedApis.Count}";
    }
}
