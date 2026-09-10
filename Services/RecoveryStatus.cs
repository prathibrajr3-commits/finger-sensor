namespace AirGestureAI.Services
{
    /// <summary>
    /// Read-only snapshot of the crash recovery status, populated at startup and
    /// exposed by <see cref="App"/> for the UI layer and diagnostic tooling.
    /// </summary>
    public sealed class RecoveryStatus
    {
        /// <summary>Gets whether Safe Mode is currently active.</summary>
        public bool IsSafeMode { get; init; }

        /// <summary>Gets whether an abnormal shutdown was detected on this startup.</summary>
        public bool AbnormalShutdownDetected { get; init; }

        /// <summary>Gets the source from which the session was recovered (e.g. "Primary", "Backup", "SafeMode").</summary>
        public string RecoverySource { get; init; } = "None";

        /// <summary>Gets the number of consecutive startup failures recorded before this run.</summary>
        public int ConsecutiveFailureCount { get; init; }

        /// <summary>Gets the overall validation severity from the last recovery sequence ("Healthy", "Warning", "Recoverable", "Critical").</summary>
        public string LastValidationSeverity { get; init; } = "Healthy";

        /// <summary>Gets the UTC timestamp of the last successful autosave, or null if none has occurred.</summary>
        public string? LastSuccessfulSaveTime { get; init; }

        /// <summary>Returns true when the application started cleanly without any recovery events.</summary>
        public bool IsCleanStart =>
            !IsSafeMode &&
            !AbnormalShutdownDetected &&
            ConsecutiveFailureCount == 0 &&
            (RecoverySource == "None" || RecoverySource == "Default");
    }
}
