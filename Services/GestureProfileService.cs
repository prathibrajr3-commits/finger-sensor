using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using AirGestureAI.Models;
using AirGestureAI.Utilities;

namespace AirGestureAI.Services
{
    // ── Data Models ───────────────────────────────────────────────────────────

    /// <summary>Specifies the kind of system action bound to a gesture.</summary>
    public enum GestureActionType
    {
        /// <summary>No action assigned.</summary>
        None,
        /// <summary>Simulates a keyboard shortcut.</summary>
        Keyboard,
        /// <summary>Launches an application or script.</summary>
        LaunchApp,
        /// <summary>Executes a system media command (play, pause, next, etc.).</summary>
        MediaControl,
        /// <summary>Invokes a registered workflow from the Workflow Engine.</summary>
        RunWorkflow,
    }

    /// <summary>Describes the action bound to a gesture in a gesture profile.</summary>
    public sealed class GestureAction
    {
        /// <summary>Gets or sets the action type.</summary>
        public GestureActionType Type { get; set; } = GestureActionType.None;

        /// <summary>Gets or sets the action parameter (keyboard shortcut, path, workflow id, etc.).</summary>
        public string Parameter { get; set; } = string.Empty;
    }

    /// <summary>Maps a single <see cref="GestureType"/> to its enabled state, sensitivity, and bound action.</summary>
    public sealed class GestureBinding
    {
        /// <summary>Gets or sets the gesture type this binding refers to.</summary>
        public GestureType Gesture { get; set; }

        /// <summary>Gets or sets whether this gesture is enabled.</summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>Gets or sets the sensitivity multiplier (0.1 – 2.0, default 1.0).</summary>
        public double Sensitivity { get; set; } = 1.0;

        /// <summary>Gets or sets the action triggered when this gesture fires.</summary>
        public GestureAction Action { get; set; } = new();
    }

    /// <summary>A named set of gesture bindings that can be saved and loaded as a profile.</summary>
    public sealed class GestureProfile
    {
        /// <summary>Gets or sets the unique profile identifier.</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>Gets or sets the display name of the profile.</summary>
        public string Name { get; set; } = "Default";

        /// <summary>Gets or sets the creation timestamp.</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Gets or sets the last modification timestamp.</summary>
        public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Gets or sets the list of gesture bindings in this profile.</summary>
        public List<GestureBinding> Bindings { get; set; } = new();
    }

    // ── Service ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Manages gesture profiles: CRUD operations, import/export, and reset-to-defaults.
    /// Profiles are persisted as JSON files under
    /// <c>%LocalAppData%\AirGestureAI\GestureProfiles\</c>.
    /// </summary>
    public sealed class GestureProfileService
    {
        private readonly string _profilesDir;
        private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

        /// <summary>Initialises the <see cref="GestureProfileService"/>.</summary>
        public GestureProfileService()
        {
            _profilesDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AirGestureAI", "GestureProfiles");
            Directory.CreateDirectory(_profilesDir);
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Asynchronously loads all saved gesture profiles.
        /// Returns an empty list when no profiles exist yet.
        /// </summary>
        public async Task<List<GestureProfile>> LoadAllProfilesAsync()
        {
            var profiles = new List<GestureProfile>();
            try
            {
                foreach (var file in Directory.GetFiles(_profilesDir, "*.json"))
                {
                    try
                    {
                        var json    = await File.ReadAllTextAsync(file);
                        var profile = JsonSerializer.Deserialize<GestureProfile>(json, _jsonOptions);
                        if (profile is not null) profiles.Add(profile);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"GestureProfileService: Failed to load profile {file}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("GestureProfileService: LoadAllProfilesAsync failed", ex);
            }
            return profiles;
        }

        /// <summary>
        /// Asynchronously saves a <see cref="GestureProfile"/> to disk.
        /// Creates or overwrites the profile JSON file.
        /// </summary>
        /// <param name="profile">The profile to persist.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="profile"/> is null.</exception>
        public async Task SaveProfileAsync(GestureProfile profile)
        {
            if (profile is null) throw new ArgumentNullException(nameof(profile));
            profile.ModifiedAt = DateTime.UtcNow;
            var path = Path.Combine(_profilesDir, $"{profile.Id}.json");
            var json = JsonSerializer.Serialize(profile, _jsonOptions);
            await File.WriteAllTextAsync(path, json);
            Logger.Info($"GestureProfileService: Profile '{profile.Name}' saved to {path}");
        }

        /// <summary>Deletes the profile with the given <paramref name="profileId"/> from disk.</summary>
        /// <param name="profileId">The <see cref="GestureProfile.Id"/> to delete.</param>
        public void DeleteProfile(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId)) return;
            var path = Path.Combine(_profilesDir, $"{profileId}.json");
            if (File.Exists(path))
            {
                File.Delete(path);
                Logger.Info($"GestureProfileService: Deleted profile {profileId}");
            }
        }

        /// <summary>
        /// Exports a <see cref="GestureProfile"/> to a caller-specified <paramref name="filePath"/>.
        /// </summary>
        public async Task ExportProfileAsync(GestureProfile profile, string filePath)
        {
            if (profile  is null)                     throw new ArgumentNullException(nameof(profile));
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));

            var json = JsonSerializer.Serialize(profile, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json);
            Logger.Info($"GestureProfileService: Profile exported to {filePath}");
        }

        /// <summary>
        /// Imports a <see cref="GestureProfile"/> from <paramref name="filePath"/> and saves it.
        /// </summary>
        /// <returns>The imported <see cref="GestureProfile"/>.</returns>
        public async Task<GestureProfile> ImportProfileAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));

            var json    = await File.ReadAllTextAsync(filePath);
            var profile = JsonSerializer.Deserialize<GestureProfile>(json, _jsonOptions)
                          ?? throw new InvalidOperationException("File does not contain a valid gesture profile.");

            // Assign a new id to prevent collisions with existing profiles
            profile.Id         = Guid.NewGuid().ToString();
            profile.ModifiedAt = DateTime.UtcNow;

            await SaveProfileAsync(profile);
            Logger.Info($"GestureProfileService: Profile imported from {filePath}");
            return profile;
        }

        /// <summary>
        /// Builds and returns the factory-default <see cref="GestureProfile"/> without persisting it.
        /// </summary>
        public GestureProfile BuildDefaultProfile() => new()
        {
            Name     = "Default",
            Bindings = new List<GestureBinding>
            {
                new() { Gesture = GestureType.ScrollUp,   IsEnabled = true,  Sensitivity = 1.0,
                         Action = new() { Type = GestureActionType.MediaControl, Parameter = "scroll_up" } },
                new() { Gesture = GestureType.ScrollDown, IsEnabled = true,  Sensitivity = 1.0,
                         Action = new() { Type = GestureActionType.MediaControl, Parameter = "scroll_down" } },
                new() { Gesture = GestureType.OpenPalm,  IsEnabled = true,  Sensitivity = 1.0,
                         Action = new() { Type = GestureActionType.Keyboard,     Parameter = "Space" } },
            }
        };
    }
}
