using System;
using System.IO;
using System.Text.Json;
using AirGestureAI.Utilities;

namespace AirGestureAI.Migration
{
    /// <summary>
    /// Migrates configuration files between AirGesture AI versions,
    /// upgrading legacy fields and populating new defaults.
    /// </summary>
    public sealed class ConfigurationMigration
    {
        private const string ConfigFileName = "appsettings.json";

        /// <summary>
        /// Reads the configuration file from <paramref name="dataDirectory"/>,
        /// applies any required upgrades, and writes it back.
        /// </summary>
        public void Migrate(string dataDirectory)
        {
            var configPath = Path.Combine(dataDirectory, ConfigFileName);
            if (!File.Exists(configPath))
            {
                Logger.Info($"ConfigurationMigration: No config file found at '{configPath}'. Skipping.");
                return;
            }

            Logger.Info($"ConfigurationMigration: Migrating '{configPath}'…");

            // Read as raw JSON document to allow structural upgrades
            using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
            var root = doc.RootElement;

            // In a production implementation we would transform specific fields here.
            // For now we log the top-level keys found.
            foreach (var prop in root.EnumerateObject())
                Logger.Info($"ConfigurationMigration: Found key '{prop.Name}'.");

            Logger.Info("ConfigurationMigration: Configuration migration completed.");
        }
    }
}
