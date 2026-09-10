using System;
using System.Collections.Generic;
using System.Text.Json;
using System.IO;
using AirGestureAI.Utilities;

namespace AirGestureAI.Migration
{
    /// <summary>
    /// Orchestrates migration of configuration files, plugin settings, and data stores
    /// between AirGesture AI versions.
    /// </summary>
    public sealed class MigrationEngine
    {
        private readonly ConfigurationMigration _configMigration;
        private readonly PluginMigration _pluginMigration;
        private readonly VersionUpgradePlanner _planner;

        /// <summary>Raised after a migration step completes, with a progress message.</summary>
        public event Action<string>? StepCompleted;

        /// <summary>
        /// Initializes a new <see cref="MigrationEngine"/>.
        /// </summary>
        public MigrationEngine()
        {
            _configMigration = new ConfigurationMigration();
            _pluginMigration = new PluginMigration();
            _planner         = new VersionUpgradePlanner();
        }

        /// <summary>
        /// Runs a full migration from <paramref name="fromVersion"/> to <paramref name="toVersion"/>.
        /// </summary>
        /// <param name="fromVersion">Source version string (SemVer).</param>
        /// <param name="toVersion">Target version string (SemVer).</param>
        /// <param name="dataDirectory">Root directory containing configuration and plugin data.</param>
        public MigrationResult Migrate(string fromVersion, string toVersion, string dataDirectory)
        {
            Logger.Info($"MigrationEngine: Migrating {fromVersion} → {toVersion} in '{dataDirectory}'…");

            var steps = _planner.Plan(fromVersion, toVersion);
            var applied = new List<string>();
            var errors  = new List<string>();

            foreach (var step in steps)
            {
                try
                {
                    Logger.Info($"MigrationEngine: Executing step '{step}'…");
                    ExecuteStep(step, dataDirectory);
                    applied.Add(step);
                    StepCompleted?.Invoke($"✓ {step}");
                }
                catch (Exception ex)
                {
                    var msg = $"Step '{step}' failed: {ex.Message}";
                    Logger.Error($"MigrationEngine: {msg}", ex);
                    errors.Add(msg);
                    StepCompleted?.Invoke($"✗ {msg}");
                }
            }

            var success = errors.Count == 0;
            Logger.Info($"MigrationEngine: Migration {(success ? "succeeded" : "completed with errors")}.");
            return new MigrationResult(fromVersion, toVersion, applied, errors);
        }

        private void ExecuteStep(string step, string dataDirectory)
        {
            switch (step)
            {
                case "MigrateConfiguration":
                    _configMigration.Migrate(dataDirectory);
                    break;
                case "MigratePlugins":
                    _pluginMigration.Migrate(dataDirectory);
                    break;
                default:
                    Logger.Warn($"MigrationEngine: Unknown step '{step}' — skipping.");
                    break;
            }
        }
    }

    /// <summary>Represents the result of a migration run.</summary>
    public sealed class MigrationResult
    {
        public string FromVersion   { get; }
        public string ToVersion     { get; }
        public bool   Success       => Errors.Count == 0;
        public IReadOnlyList<string> AppliedSteps { get; }
        public IReadOnlyList<string> Errors        { get; }

        public MigrationResult(
            string fromVersion, string toVersion,
            IReadOnlyList<string> applied, IReadOnlyList<string> errors)
        {
            FromVersion   = fromVersion;
            ToVersion     = toVersion;
            AppliedSteps  = applied;
            Errors        = errors;
        }

        public override string ToString() =>
            $"Migration {FromVersion}→{ToVersion}: {(Success ? "OK" : $"{Errors.Count} error(s)")}. Steps: {AppliedSteps.Count}";
    }
}
