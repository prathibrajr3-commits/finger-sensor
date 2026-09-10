using System;
using System.Collections.Generic;

namespace AirGestureAI.SDK
{
    /// <summary>
    /// Validates SDK version compatibility between the host and an extension or plugin.
    /// </summary>
    public sealed class SdkCompatibility
    {
        private readonly SdkVersion _hostVersion;
        private readonly HashSet<string> _deprecatedApis;

        /// <summary>
        /// Initializes a new <see cref="SdkCompatibility"/> validator.
        /// </summary>
        /// <param name="hostVersion">The SDK version provided by the host.</param>
        public SdkCompatibility(SdkVersion hostVersion)
        {
            _hostVersion    = hostVersion ?? throw new ArgumentNullException(nameof(hostVersion));
            _deprecatedApis = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Registers an API name that has been deprecated and should be reported during checks.
        /// </summary>
        public void RegisterDeprecatedApi(string apiName) =>
            _deprecatedApis.Add(apiName);

        /// <summary>
        /// Checks whether the host is compatible with a required minimum version.
        /// </summary>
        /// <param name="minimumVersionStr">Minimum version string (e.g. "1.0.0").</param>
        /// <returns>A <see cref="CompatibilityCheckResult"/> with details.</returns>
        public CompatibilityCheckResult Check(string minimumVersionStr)
        {
            if (!SdkVersion.TryParse(minimumVersionStr, out var required) || required is null)
            {
                return CompatibilityCheckResult.Failure(
                    $"Cannot parse required version string '{minimumVersionStr}'.");
            }

            if (!_hostVersion.IsCompatibleWith(required))
            {
                return CompatibilityCheckResult.Failure(
                    $"Host SDK v{_hostVersion} is not compatible with required v{required}. " +
                    $"Major versions must match and host must be >= required.");
            }

            return CompatibilityCheckResult.Success(_hostVersion);
        }

        /// <summary>
        /// Returns which of the provided API names are deprecated.
        /// </summary>
        public IReadOnlyList<string> FindDeprecatedUsages(IEnumerable<string> usedApis)
        {
            var found = new List<string>();
            foreach (var api in usedApis)
                if (_deprecatedApis.Contains(api)) found.Add(api);
            return found;
        }
    }

    /// <summary>Represents the result of a compatibility check.</summary>
    public sealed class CompatibilityCheckResult
    {
        /// <summary>Gets whether the check passed.</summary>
        public bool IsCompatible { get; private init; }

        /// <summary>Gets the resolved host version (populated on success).</summary>
        public SdkVersion? HostVersion { get; private init; }

        /// <summary>Gets a human-readable reason (populated on failure).</summary>
        public string? Reason { get; private init; }

        private CompatibilityCheckResult() { }

        internal static CompatibilityCheckResult Success(SdkVersion host) =>
            new() { IsCompatible = true, HostVersion = host };

        internal static CompatibilityCheckResult Failure(string reason) =>
            new() { IsCompatible = false, Reason = reason };

        /// <inheritdoc/>
        public override string ToString() =>
            IsCompatible ? $"Compatible (host v{HostVersion})" : $"Incompatible: {Reason}";
    }
}
