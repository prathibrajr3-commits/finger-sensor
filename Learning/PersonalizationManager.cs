using System;
using System.IO;

namespace AirGestureAI.Learning
{
    /// <summary>
    /// Manages active personalization profiles and provides profile serialization.
    /// </summary>
    public class PersonalizationManager
    {
        private LearningProfile _currentProfile = new();

        /// <summary>Gets the currently active personalization profile.</summary>
        public LearningProfile CurrentProfile => _currentProfile;

        /// <summary>
        /// Resets the active personalization profile back to defaults.
        /// </summary>
        public void ResetProfile()
        {
            _currentProfile = new LearningProfile();
        }

        /// <summary>
        /// Loads a personalization profile from the local filesystem.
        /// </summary>
        public void LoadProfile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));
            if (!File.Exists(filePath)) return;

            // Simple profile loading representation
            _currentProfile = new LearningProfile { ProfileName = Path.GetFileNameWithoutExtension(filePath) };
        }

        /// <summary>
        /// Saves the active personalization profile to the local filesystem.
        /// </summary>
        public void SaveProfile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            File.WriteAllText(filePath, $"// AirGesture AI Personalization Profile: {_currentProfile.ProfileName}");
        }
    }
}
