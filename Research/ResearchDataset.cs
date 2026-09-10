using System;
using System.Collections.Generic;
using AirGestureAI.Models;

namespace AirGestureAI.Research
{
    /// <summary>
    /// Represents a dataset of collected gesture coordinates, landmark configurations, and labels used for model training and validation.
    /// </summary>
    public class ResearchDataset
    {
        /// <summary>Gets the unique ID of the dataset.</summary>
        public Guid Id { get; init; } = Guid.NewGuid();

        /// <summary>Gets or sets the dataset name.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets or sets the list of hand tracking frames recorded for research.</summary>
        public List<HandData> Samples { get; set; } = new();

        /// <summary>Gets or sets corresponding label annotations (e.g. gesture names) for each sample frame.</summary>
        public List<string> Labels { get; set; } = new();

        /// <summary>
        /// Adds a hand tracking sample and its corresponding gesture label to the dataset.
        /// </summary>
        public void AddSample(HandData handData, string label)
        {
            if (handData == null) throw new ArgumentNullException(nameof(handData));
            Samples.Add(handData);
            Labels.Add(label ?? "Unknown");
        }

        /// <summary>
        /// Clears all recorded samples in the dataset.
        /// </summary>
        public void Clear()
        {
            Samples.Clear();
            Labels.Clear();
        }
    }
}
