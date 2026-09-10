using System;
using System.Collections.Generic;

namespace AirGestureAI.ModelZoo
{
    /// <summary>
    /// Maintains the local registry of all installed and available AI models.
    /// </summary>
    public sealed class ModelCatalog
    {
        private readonly List<ModelMetadata> _models = new();

        /// <summary>Gets all models currently registered in the catalog.</summary>
        public IReadOnlyList<ModelMetadata> Models => _models;

        /// <summary>
        /// Initializes a new instance of <see cref="ModelCatalog"/> with built-in stub models.
        /// </summary>
        public ModelCatalog()
        {
            // Pre-register built-in mock models
            _models.Add(new ModelMetadata
            {
                ModelId     = "gesture_classifier_v1",
                DisplayName = "Gesture Classifier v1.0",
                Version     = "1.0.0",
                SizeMb      = 4.2,
                FilePath    = "models/gesture_classifier_v1.onnx",
                Checksum    = "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6",
                IsValidated = true
            });
            _models.Add(new ModelMetadata
            {
                ModelId     = "gesture_classifier_v2",
                DisplayName = "Gesture Classifier v2.0 (Experimental)",
                Version     = "2.0.0-beta",
                SizeMb      = 7.8,
                FilePath    = "models/gesture_classifier_v2.onnx",
                Checksum    = "b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7",
                IsValidated = false
            });
        }

        /// <summary>Registers a new model in the catalog.</summary>
        public void Register(ModelMetadata model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            _models.Add(model);
        }

        /// <summary>Retrieves a model by its identifier.</summary>
        public ModelMetadata? FindById(string modelId) =>
            _models.Find(m => m.ModelId.Equals(modelId, StringComparison.OrdinalIgnoreCase));
    }
}
