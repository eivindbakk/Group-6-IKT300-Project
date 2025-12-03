using System;
using System.IO;

namespace Microkernel.Core
{
    /// <summary>
    /// Configuration settings for the kernel. 
    /// </summary>
    public sealed class KernelConfiguration
    {
        public string PluginsDirectory { get; set; }
        public string PluginSearchPattern { get; set; } = "*.dll";
        public bool SearchSubdirectories { get; set; } = true;
        
        // If true, kernel continues even if a plugin fails to load
        public bool ContinueOnPluginError { get; set; } = true;

        /// <summary>
        /// Creates default configuration by locating the Plugins folder. 
        /// </summary>
        public static KernelConfiguration CreateDefault()
        {
            string baseDir = AppContext.BaseDirectory;
            DirectoryInfo current = new DirectoryInfo(baseDir);
            
            // Navigate up to project root (bin/Debug/net8.0 -> project root)
            DirectoryInfo projectRoot = current.Parent. Parent. Parent. Parent;
            string pluginsDir = Path.Combine(projectRoot. FullName, "Plugins");

            return new KernelConfiguration
            {
                PluginsDirectory = pluginsDir,
                PluginSearchPattern = "*.dll",
                SearchSubdirectories = true,
                ContinueOnPluginError = true
            };
        }
    }
}