using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using AirGestureAI.Utilities;

namespace AirGestureAI.Localization
{
    /// <summary>
    /// Loads and provides localized strings for the AirGesture AI UI.
    /// Language packs are JSON files stored in the application data directory.
    /// </summary>
    public sealed class LanguageManager
    {
        private static readonly IReadOnlyList<string> SupportedLanguages = new[]
        {
            "en",  // English
            "ta",  // Tamil
            "hi",  // Hindi
            "fr",  // French
            "de",  // German
            "es",  // Spanish
            "ja",  // Japanese
            "zh",  // Chinese (Simplified)
            "ar",  // Arabic
        };

        private readonly string _languagePackDirectory;
        private Dictionary<string, string> _currentStrings = new();
        private string _activeLanguage = "en";

        /// <summary>Raised after the active language changes.</summary>
        public event Action<string>? LanguageChanged;

        /// <summary>Gets the list of all supported language codes.</summary>
        public static IReadOnlyList<string> Languages => SupportedLanguages;

        /// <summary>Gets the currently active language code.</summary>
        public string ActiveLanguage => _activeLanguage;

        /// <summary>
        /// Initializes a new <see cref="LanguageManager"/>.
        /// </summary>
        /// <param name="languagePackDirectory">Directory containing language JSON files.</param>
        public LanguageManager(string languagePackDirectory)
        {
            _languagePackDirectory = languagePackDirectory
                ?? throw new ArgumentNullException(nameof(languagePackDirectory));
        }

        /// <summary>
        /// Loads the language pack for the given language code and activates it.
        /// Falls back to English if the requested language is unavailable.
        /// </summary>
        public void SetLanguage(string languageCode)
        {
            var code     = string.IsNullOrWhiteSpace(languageCode) ? "en" : languageCode.ToLowerInvariant();
            var packPath = Path.Combine(_languagePackDirectory, $"{code}.json");

            if (!File.Exists(packPath))
            {
                Logger.Warn($"LanguageManager: Pack for '{code}' not found at '{packPath}'. Falling back to 'en'.");
                code     = "en";
                packPath = Path.Combine(_languagePackDirectory, "en.json");
            }

            if (File.Exists(packPath))
            {
                try
                {
                    _currentStrings = JsonSerializer.Deserialize<Dictionary<string, string>>(
                        File.ReadAllText(packPath)) ?? new Dictionary<string, string>();
                }
                catch (Exception ex)
                {
                    Logger.Error($"LanguageManager: Failed to load language pack '{packPath}'.", ex);
                    _currentStrings = new Dictionary<string, string>();
                }
            }

            _activeLanguage = code;
            Logger.Info($"LanguageManager: Language set to '{code}'.");
            LanguageChanged?.Invoke(code);
        }

        /// <summary>
        /// Returns the localized string for the given key.
        /// Returns the key itself if no translation is found.
        /// </summary>
        public string Get(string key) =>
            _currentStrings.TryGetValue(key, out var value) ? value : key;
    }
}
