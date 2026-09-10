using System;
using AirGestureAI.SDK;

namespace AirGestureAI.Extensions
{
    /// <summary>
    /// Validates an <see cref="ExtensionManifest"/> before installation.
    /// Checks required fields, SDK version compatibility, and manifest integrity.
    /// </summary>
    public sealed class ExtensionValidator
    {
        private readonly ISdkHost _sdkHost;

        /// <summary>
        /// Initializes a new <see cref="ExtensionValidator"/>.
        /// </summary>
        public ExtensionValidator(ISdkHost sdkHost)
        {
            _sdkHost = sdkHost ?? throw new ArgumentNullException(nameof(sdkHost));
        }

        /// <summary>
        /// Validates the given manifest. Returns a result describing pass/fail.
        /// </summary>
        public ValidationResult Validate(ExtensionManifest manifest)
        {
            if (manifest is null)
                return ValidationResult.Fail("Manifest is null.");

            if (string.IsNullOrWhiteSpace(manifest.Id))
                return ValidationResult.Fail("Extension ID is required.");

            if (string.IsNullOrWhiteSpace(manifest.DisplayName))
                return ValidationResult.Fail("DisplayName is required.");

            if (string.IsNullOrWhiteSpace(manifest.EntryAssembly))
                return ValidationResult.Fail("EntryAssembly is required.");

            if (!SdkVersion.TryParse(manifest.Version, out _))
                return ValidationResult.Fail($"Extension version '{manifest.Version}' is not valid SemVer.");

            if (!_sdkHost.IsCompatible(manifest.MinSdkVersion))
                return ValidationResult.Fail(
                    $"Host SDK v{_sdkHost.Version} does not satisfy minimum version '{manifest.MinSdkVersion}'.");

            return ValidationResult.Pass();
        }
    }

    /// <summary>Result of an extension manifest validation.</summary>
    public sealed class ValidationResult
    {
        /// <summary>Gets whether the validation passed.</summary>
        public bool IsValid { get; private init; }

        /// <summary>Gets the reason for failure (null on success).</summary>
        public string? Reason { get; private init; }

        private ValidationResult() { }

        internal static ValidationResult Pass()              => new() { IsValid = true };
        internal static ValidationResult Fail(string reason) => new() { IsValid = false, Reason = reason };

        /// <inheritdoc/>
        public override string ToString() => IsValid ? "Valid" : $"Invalid: {Reason}";
    }
}
