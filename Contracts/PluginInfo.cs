using System;

namespace Contracts
{
    /// <summary>
    /// Contains basic information about a loaded plugin.
    /// Used for displaying plugin status without exposing the full IPlugin interface.
    /// </summary>
    public class PluginInfo
    {
        /// <summary>
        /// The name of the plugin. 
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The version of the plugin.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// The current state of the plugin. 
        /// </summary>
        public string State { get; set; }

        /// <summary>
        /// When the plugin was loaded.
        /// </summary>
        public DateTime LoadedAt { get; set; }
    }
}