using System;

namespace AirGestureAI.Plugins
{
    /// <summary>
    /// Core plugin lifecycle contract.
    /// Every plugin must implement this interface.
    /// </summary>
    public interface IPlugin : IDisposable
    {
        /// <summary>Gets the plugin metadata descriptor.</summary>
        PluginDescriptor Descriptor { get; }

        /// <summary>
        /// Called once after the plugin is loaded. Allows the plugin to
        /// acquire resources, read configuration, and prepare internal state.
        /// Must not throw; return false to abort loading.
        /// </summary>
        bool Initialize();

        /// <summary>
        /// Called when AirGesture AI begins a tracking session.
        /// </summary>
        void Start();

        /// <summary>
        /// Called when AirGesture AI ends a tracking session or is shutting down.
        /// </summary>
        void Stop();
    }
}
