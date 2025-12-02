using System;

namespace Microkernel.Core
{
    public class PluginInfo
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public string State { get; set; }
        public DateTime LoadedAt { get; set; }
        public int ProcessId { get; set; }
    }
}