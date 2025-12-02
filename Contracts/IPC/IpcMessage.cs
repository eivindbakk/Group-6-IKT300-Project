using System;

namespace Contracts.IPC
{
    /// <summary>
    /// Message format for IPC communication between kernel and plugin processes.
    /// </summary>
    public class IpcMessage
    {
        public IpcMessageType Type { get; set; }
        public string PluginName { get; set; }
        public EventMessage Event { get; set; }
        public string Command { get; set; }
        public string Response { get; set; }
        public DateTime Timestamp { get; set; }

        public IpcMessage()
        {
            Timestamp = DateTime. UtcNow;
        }
    }

    public enum IpcMessageType
    {
        Start,
        Stop,
        Event,
        Publish,
        Heartbeat,
        Shutdown,
        Ack
    }
}