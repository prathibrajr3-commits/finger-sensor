using AirGestureAI.Models;

namespace AirGestureAI.Plugins
{
    /// <summary>
    /// Extends <see cref="IPlugin"/> for plugins that handle application-specific
    /// gesture translation and action overrides.
    /// </summary>
    public interface IApplicationAdapter : IPlugin
    {
        /// <summary>
        /// Called whenever the foreground application context changes.
        /// Adapters should update their internal state to reflect the new context.
        /// </summary>
        /// <param name="context">The new application context snapshot.</param>
        void OnApplicationChanged(ApplicationContext context);

        /// <summary>
        /// Routes an incoming gesture through the adapter for context-aware handling.
        /// </summary>
        /// <param name="gesture">The recognized gesture type.</param>
        /// <param name="context">The current application context.</param>
        /// <param name="overrideDefault">
        /// Set to <see langword="true"/> if the adapter handled the gesture and
        /// the default AirGesture action should be suppressed.
        /// </param>
        /// <returns>
        /// An optional human-readable description of the action taken,
        /// or <see langword="null"/> if the adapter did not handle the gesture.
        /// </returns>
        string? ProcessGesture(GestureType gesture, ApplicationContext context, out bool overrideDefault);
    }
}
