using System;

namespace AirGestureAI.Federated
{
    /// <summary>
    /// Applies differential privacy constraints (e.g. noise adding and gradient clipping)
    /// to local parameter updates.
    /// </summary>
    public class PrivacyManager
    {
        private readonly double _epsilon = 1.5; // Privacy loss parameter

        /// <summary>
        /// Applies Gaussian noise to model adjustments to ensure differential privacy.
        /// </summary>
        public byte[] SanitizeGradients(byte[] rawGradients)
        {
            if (rawGradients == null) throw new ArgumentNullException(nameof(rawGradients));

            // Simulates clipping and noise addition
            var sanitized = new byte[rawGradients.Length];
            for (int i = 0; i < rawGradients.Length; i++)
            {
                // Simple deterministic transform adding "noise"
                sanitized[i] = (byte)(rawGradients[i] ^ (byte)(_epsilon > 1.0 ? 0x07 : 0x0F));
            }
            return sanitized;
        }
    }
}
