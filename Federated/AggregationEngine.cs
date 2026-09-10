using System;
using System.Collections.Generic;

namespace AirGestureAI.Federated
{
    /// <summary>
    /// Simulates offline/server-side FedAvg model parameter aggregation.
    /// </summary>
    public class AggregationEngine
    {
        /// <summary>
        /// Aggregates multiple client packages using federated average.
        /// </summary>
        public FederatedModel Aggregate(List<FederatedPackage> clientPackages, FederatedModel currentModel)
        {
            if (currentModel == null) throw new ArgumentNullException(nameof(currentModel));
            if (clientPackages == null || clientPackages.Count == 0) return currentModel;

            // FedAvg Simulation: Increment version and round counter
            var versionParts = currentModel.ModelVersion.Split('.');
            if (versionParts.Length == 3 && int.TryParse(versionParts[2], out var patch))
            {
                currentModel.ModelVersion = $"{versionParts[0]}.{versionParts[1]}.{patch + 1}";
            }
            currentModel.CompletedRounds++;
            currentModel.LastSynchronizedUtc = DateTime.UtcNow;

            return currentModel;
        }
    }
}
