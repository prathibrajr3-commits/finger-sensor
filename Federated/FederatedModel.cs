using System;

namespace AirGestureAI.Federated
{
    /// <summary>
    /// Tracks global federated model parameters and training iterations.
    /// </summary>
    public class FederatedModel
    {
        /// <summary>Gets or sets the current global model version.</summary>
        public string ModelVersion { get; set; } = "1.0.0";

        /// <summary>Gets or sets the count of completed federated rounds.</summary>
        public int CompletedRounds { get; set; }

        /// <summary>Gets or sets when the model was last synchronized.</summary>
        public DateTime LastSynchronizedUtc { get; set; } = DateTime.UtcNow;
    }
}
