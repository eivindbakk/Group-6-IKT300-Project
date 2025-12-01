using System. Collections.Generic;
using System.IO;
using Contracts;

namespace Microkernel.Plugins
{
    /// <summary>
    /// Interface for plugin loading strategy.
    /// Allows different loading mechanisms (reflection, AssemblyLoadContext, etc.)
    /// </summary>
    public interface IPluginLoader
    {
        /// <summary>
        /// Load plugins from specified directory.
        /// </summary>
        /// <param name="pluginsDirectory">Directory containing plugin assemblies. </param>
        /// <param name="searchPattern">File search pattern (e.g., "*.dll").</param>
        /// <param name="searchOption">Whether to search subdirectories.</param>
        /// <returns>Collection of loaded plugin instances.</returns>
        IEnumerable<IPlugin> LoadPlugins(string pluginsDirectory, string searchPattern, SearchOption searchOption);
    }
}