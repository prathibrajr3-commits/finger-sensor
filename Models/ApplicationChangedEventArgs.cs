using System;
using AirGestureAI.Models;

namespace AirGestureAI.Models
{
    /// <summary>
    /// Event arguments describing a foreground application context change.
    /// </summary>
    public sealed class ApplicationChangedEventArgs : EventArgs
    {
        /// <summary>Gets the previous context (null on first detection).</summary>
        public ApplicationContext? OldContext { get; }

        /// <summary>Gets the newly detected context.</summary>
        public ApplicationContext NewContext { get; }

        /// <summary>
        /// True when only the window title changed (same process/handle), 
        /// e.g. a browser tab navigation.
        /// </summary>
        public bool IsTitleChangeOnly { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="ApplicationChangedEventArgs"/>.
        /// </summary>
        public ApplicationChangedEventArgs(
            ApplicationContext? oldContext,
            ApplicationContext newContext,
            bool isTitleChangeOnly = false)
        {
            OldContext       = oldContext;
            NewContext       = newContext;
            IsTitleChangeOnly = isTitleChangeOnly;
        }
    }
}
