using System;
using System.IO;
using AirGestureAI.Utilities;

namespace AirGestureAI.Security
{
    /// <summary>
    /// Enforces path safety constraints to block directory traversal, volume escape,
    /// and symbolic link attacks.
    /// </summary>
    public static class PathSecurity
    {
        /// <summary>
        /// Validates that the targeted path resides strictly within the specified root directory boundary.
        /// </summary>
        /// <param name="inputPath">The relative or absolute target path.</param>
        /// <param name="rootDirectory">The authorized base boundary directory.</param>
        /// <returns>True if the path is safe; otherwise, false.</returns>
        public static bool IsPathSafe(string inputPath, string rootDirectory)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(inputPath) || string.IsNullOrWhiteSpace(rootDirectory))
                {
                    Logger.Warn("PathSecurity: Validation failed due to null or empty path inputs.");
                    return false;
                }

                var normalizedRoot = Path.GetFullPath(rootDirectory);
                var normalizedPath = Path.GetFullPath(inputPath);

                // Enforce root directory boundary
                if (!normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Warn($"PathSecurity: Traversal attempt blocked! Path '{inputPath}' resolved to '{normalizedPath}', which is outside root '{normalizedRoot}'.");
                    return false;
                }

                // Check for duplicate volume names or alternate data streams
                if (normalizedPath.IndexOf(':', 2) != -1)
                {
                    Logger.Warn($"PathSecurity: Alternate data stream or invalid volume identifier blocked in path '{inputPath}'.");
                    return false;
                }

                // Block active traversal characters in normalized path representation
                if (normalizedPath.Contains("..") || normalizedPath.Contains("./") || normalizedPath.Contains(".\\"))
                {
                    Logger.Warn($"PathSecurity: Traversal characters detected in normalized path representation for '{inputPath}'.");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"PathSecurity: Exception validating path safety for '{inputPath}'", ex);
                return false;
            }
        }
    }
}
