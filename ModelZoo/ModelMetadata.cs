using System;

namespace AirGestureAI.ModelZoo
{
    /// <summary>
    /// Describes an AI/ONNX model available in the local Model Zoo catalog.
    /// </summary>
    public class ModelMetadata
    {
        /// <summary>Gets the unique model identifier key.</summary>
        public string ModelId { get; init; } = string.Empty;

        /// <summary>Gets the human-readable display name.</summary>
        public string DisplayName { get; init; } = string.Empty;

        /// <summary>Gets the version of the model.</summary>
        public string Version { get; init; } = "1.0.0";

        /// <summary>Gets the file size in megabytes.</summary>
        public double SizeMb { get; init; }

        /// <summary>Gets the file path to the local model artifact.</summary>
        public string FilePath { get; init; } = string.Empty;

        /// <summary>Gets the SHA-256 checksum for integrity validation.</summary>
        public string Checksum { get; init; } = string.Empty;

        /// <summary>Gets whether this model has been validated locally.</summary>
        public bool IsValidated { get; set; }
    }
}
