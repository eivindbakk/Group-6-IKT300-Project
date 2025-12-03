using System;

namespace Microkernel.Core
{
    /// <summary>
    /// Information about a loaded plugin, used for status display.
    /// </summary>
    public class PluginInfo
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public string State { get; set; }
        public DateTime LoadedAt { get; set; }
        public int ProcessId { get; set; }
    }
}