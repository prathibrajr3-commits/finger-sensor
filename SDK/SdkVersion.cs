using System;

namespace AirGestureAI.SDK
{
    /// <summary>
    /// Represents a Semantic Versioning (SemVer) version for the AirGesture AI SDK.
    /// Supports parsing, comparison, and compatibility checking.
    /// </summary>
    public sealed class SdkVersion : IComparable<SdkVersion>, IEquatable<SdkVersion>
    {
        /// <summary>Gets the major version number. Breaking changes increment this.</summary>
        public int Major { get; }

        /// <summary>Gets the minor version number. New features increment this.</summary>
        public int Minor { get; }

        /// <summary>Gets the patch version number. Bug fixes increment this.</summary>
        public int Patch { get; }

        /// <summary>Gets an optional pre-release label (e.g. "rc1", "beta").</summary>
        public string PreRelease { get; }

        /// <summary>
        /// Initializes a new <see cref="SdkVersion"/>.
        /// </summary>
        public SdkVersion(int major, int minor, int patch, string preRelease = "")
        {
            if (major < 0) throw new ArgumentOutOfRangeException(nameof(major));
            if (minor < 0) throw new ArgumentOutOfRangeException(nameof(minor));
            if (patch < 0) throw new ArgumentOutOfRangeException(nameof(patch));
            Major = major;
            Minor = minor;
            Patch = patch;
            PreRelease = preRelease ?? string.Empty;
        }

        /// <summary>
        /// Parses a SemVer string such as "1.4.0" or "2.0.0-rc1".
        /// </summary>
        /// <exception cref="FormatException">Thrown when the string is not valid SemVer.</exception>
        public static SdkVersion Parse(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
                throw new ArgumentNullException(nameof(version));

            var preRelease = string.Empty;
            var dashIndex  = version.IndexOf('-');
            if (dashIndex >= 0)
            {
                preRelease = version[(dashIndex + 1)..];
                version    = version[..dashIndex];
            }

            var parts = version.Split('.');
            if (parts.Length != 3 ||
                !int.TryParse(parts[0], out var major) ||
                !int.TryParse(parts[1], out var minor) ||
                !int.TryParse(parts[2], out var patch))
            {
                throw new FormatException($"'{version}' is not a valid SemVer string.");
            }

            return new SdkVersion(major, minor, patch, preRelease);
        }

        /// <summary>
        /// Attempts to parse a SemVer string. Returns false on failure.
        /// </summary>
        public static bool TryParse(string version, out SdkVersion? result)
        {
            try { result = Parse(version); return true; }
            catch { result = null; return false; }
        }

        /// <summary>
        /// Returns true if this version satisfies a minimum version requirement.
        /// Compatible means same Major and this version >= required.
        /// </summary>
        public bool IsCompatibleWith(SdkVersion required)
        {
            if (required.Major != Major) return false;
            return CompareTo(required) >= 0;
        }

        /// <inheritdoc/>
        public int CompareTo(SdkVersion? other)
        {
            if (other is null) return 1;
            var cmp = Major.CompareTo(other.Major);
            if (cmp != 0) return cmp;
            cmp = Minor.CompareTo(other.Minor);
            if (cmp != 0) return cmp;
            return Patch.CompareTo(other.Patch);
        }

        /// <inheritdoc/>
        public bool Equals(SdkVersion? other) =>
            other is not null && Major == other.Major && Minor == other.Minor && Patch == other.Patch;

        /// <inheritdoc/>
        public override bool Equals(object? obj) => Equals(obj as SdkVersion);

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(Major, Minor, Patch);

        /// <inheritdoc/>
        public override string ToString() =>
            string.IsNullOrEmpty(PreRelease) ? $"{Major}.{Minor}.{Patch}" : $"{Major}.{Minor}.{Patch}-{PreRelease}";

        public static bool operator ==(SdkVersion? a, SdkVersion? b) => Equals(a, b);
        public static bool operator !=(SdkVersion? a, SdkVersion? b) => !Equals(a, b);
        public static bool operator  >(SdkVersion a, SdkVersion b)  => a.CompareTo(b)  > 0;
        public static bool operator  <(SdkVersion a, SdkVersion b)  => a.CompareTo(b)  < 0;
        public static bool operator >=(SdkVersion a, SdkVersion b)  => a.CompareTo(b) >= 0;
        public static bool operator <=(SdkVersion a, SdkVersion b)  => a.CompareTo(b) <= 0;
    }
}
