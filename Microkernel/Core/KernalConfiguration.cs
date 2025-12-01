using System;
using System. IO;

namespace Microkernel.Core
{
    public sealed class KernelConfiguration
    {
        public string PluginsDirectory { get; set; }
        public string PluginSearchPattern { get; set; } = "*.dll";
        public bool SearchSubdirectories { get; set; } = true;
        public TimeSpan PluginStartTimeout { get; set; } = TimeSpan.FromSeconds(30);
        public TimeSpan PluginStopTimeout { get; set; } = TimeSpan.FromSeconds(10);
        public bool ContinueOnPluginError { get; set; } = true;

        public static KernelConfiguration CreateDefault()
        {
            string baseDir = AppContext.BaseDirectory;
            DirectoryInfo current = new DirectoryInfo(baseDir);
            DirectoryInfo projectRoot = current.Parent. Parent. Parent. Parent;
            string pluginsDir = Path.Combine(projectRoot.FullName, "Plugins");

            return new KernelConfiguration
            {
                PluginsDirectory = pluginsDir,
                PluginSearchPattern = "*.dll",
                SearchSubdirectories = true,
                ContinueOnPluginError = true
            };
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(PluginsDirectory))
            {
                throw new InvalidOperationException("PluginsDirectory must be specified.");
            }

            if (PluginStartTimeout <= TimeSpan.Zero)
            {
                throw new InvalidOperationException("PluginStartTimeout must be positive.");
            }

            if (PluginStopTimeout <= TimeSpan.Zero)
            {
                throw new InvalidOperationException("PluginStopTimeout must be positive.");
            }
        }
    }
}