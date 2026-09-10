using System;
using AirGestureAI.Plugins;
using AirGestureAI.Models;

namespace GesturePluginTemplate
{
    /// <summary>
    /// A sample implementation of IPlugin showing how third-party developers
    /// can build custom gesture behaviors using the AirGesture AI SDK.
    /// </summary>
    public sealed class GesturePluginTemplate : IPlugin
    {
        private IPluginHost? _host;

        public PluginDescriptor Descriptor { get; } = new()
        {
            Id = "com.developer.gestureplugintemplate",
            Name = "Gesture Plugin Template",
            Description = "A starter template for building AirGesture AI plugins.",
            Version = new Version(1, 0, 0),
            MinCoreVersion = new Version(1, 1, 0),
            Author = "Developer Team",
        };

        public bool Initialize()
        {
            // Perform one-time setup, load configuration, or register custom triggers
            return true;
        }

        public void Start()
        {
            // Subscribe to hand tracking coordinates or gesture events here
        }

        public void Stop()
        {
            // Unsubscribe and release resources
        }
    }
}
