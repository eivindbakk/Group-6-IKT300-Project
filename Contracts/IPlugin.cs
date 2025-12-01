using System;

namespace Contracts
{
    /// <summary>
    /// Base interface for all plugins. 
    /// </summary>
    public interface IPlugin
    {
        /// <summary>
        /// Gets the unique name of the plugin. 
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the version of the plugin. 
        /// </summary>
        Version Version { get; }

        /// <summary>
        /// Gets a short description of what the plugin does. 
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Called when the plugin is started.
        /// </summary>
        void Start(IPluginHost host);

        /// <summary>
        /// Called when the plugin is stopped.
        /// </summary>
        void Stop();

        /// <summary>
        /// Called when an event is received.
        /// </summary>
        void HandleEvent(EventMessage evt);

        /// <summary>
        /// Gets the help information for this plugin.
        /// </summary>
        PluginHelpInfo GetHelp();
    }
}