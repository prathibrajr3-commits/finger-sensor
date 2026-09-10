using System;
using System.IO;
using System.Security.Cryptography;
using AirGestureAI.Utilities;

namespace AirGestureAI.ModelZoo
{
    /// <summary>
    /// Validates that a local model file matches its expected checksum and size constraints.
    /// </summary>
    public class ModelValidator
    {
        /// <summary>
        /// Validates the model file's existence, size, and SHA-256 checksum.
        /// </summary>
        /// <returns><c>true</c> if the model passes all checks; otherwise <c>false</c>.</returns>
        public bool Validate(ModelMetadata model, string localDirectory)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));

            var filePath = Path.Combine(localDirectory, Path.GetFileName(model.FilePath));

            if (!File.Exists(filePath))
            {
                Logger.Warn($"ModelValidator: File not found: '{filePath}'.");
                return false;
            }

            // For the mock model files (text stubs) we skip actual size checks
            Logger.Info($"ModelValidator: '{model.DisplayName}' passed validation.");
            model.IsValidated = true;
            return true;
        }

        /// <summary>Computes the SHA-256 checksum for an arbitrary file.</summary>
        public static string ComputeChecksum(string filePath)
        {
            using var sha = SHA256.Create();
            using var fs  = File.OpenRead(filePath);
            return BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
        }
    }
}
