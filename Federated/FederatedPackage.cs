using System;
using System.Collections.Generic;

namespace AirGestureAI.Federated
{
    /// <summary>
    /// Represents the secure payload containing locally computed model gradient adjustments.
    /// </summary>
    public class FederatedPackage
    {
        /// <summary>Gets the unique ID of the package.</summary>
        public Guid PackageId { get; init; } = Guid.NewGuid();

        /// <summary>Gets the local model version hash.</summary>
        public string SourceModelVersion { get; init; } = string.Empty;

        /// <summary>Gets the count of sample weights included.</summary>
        public int SampleCount { get; init; }

        /// <summary>Gets or sets the encrypted gradient updates.</summary>
        public byte[] EncryptedPayload { get; set; } = Array.Empty<byte>();
    }
}
