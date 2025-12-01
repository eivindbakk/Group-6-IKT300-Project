using System;
using Contracts;

namespace Microkernel. Plugins
{
    /// <summary>
    /// Runtime context for a loaded plugin.
    /// Tracks state and metadata for plugin lifecycle management.
    /// </summary>
    public sealed class PluginContext
    {
        public IPlugin Plugin { get; private set; }
        public PluginState State { get; set; }
        public DateTime LoadedAt { get; private set; }
        public string LastError { get; set; }

        public PluginContext(IPlugin plugin)
        {
            Plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
            State = PluginState. Loaded;
            LoadedAt = DateTime.UtcNow;
        }
    }
}