using System;

namespace Contracts.IPC
{
    /// <summary>
    /// Message format for IPC communication between kernel and plugin processes.
    /// This is the envelope that wraps all communication over named pipes.
    /// </summary>
    public class IpcMessage
    {
        /// <summary>
        /// Type of message being sent (Start, Stop, Event, etc.). 
        /// </summary>
        public IpcMessageType Type { get; set; }

        /// <summary>
        /// Name of the plugin sending or receiving the message.  
        /// </summary>
        public string PluginName { get; set; }

        /// <summary>
        /// Event data (used when Type is Event or Publish).
        /// </summary>
        public EventMessage Event { get; set; }

        /// <summary>
        /// Command string (used for custom commands).
        /// </summary>
        public string Command { get; set; }

        /// <summary>
        /// Response string (used for Ack messages).
        /// </summary>
        public string Response { get; set; }

        /// <summary>
        /// Error message (used when Type is Error).  
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Error code for categorizing errors (used when Type is Error). 
        /// </summary>
        public int ErrorCode { get; set; }

        /// <summary>
        /// When the message was created.  
        /// </summary>
        public DateTime Timestamp { get; set; }

        public IpcMessage()
        {
            Timestamp = DateTime. UtcNow;
        }

        /// <summary>
        /// Factory method to create an error message.
        /// </summary>
        public static IpcMessage CreateError(string pluginName, string errorMessage, int errorCode = 0)
        {
            return new IpcMessage
            {
                Type = IpcMessageType.Error,
                PluginName = pluginName,
                ErrorMessage = errorMessage,
                ErrorCode = errorCode
            };
        }
    }

    /// <summary>
    /// Types of IPC messages exchanged between kernel and plugins.
    /// Defines the protocol for kernel-plugin communication.
    /// </summary>
    public enum IpcMessageType
    {
        /// <summary>
        /// Kernel tells plugin to start/initialize. 
        /// </summary>
        Start,

        /// <summary>
        /// Kernel tells plugin to stop (but not exit).
        /// </summary>
        Stop,

        /// <summary>
        /// Kernel sends an event to the plugin.
        /// </summary>
        Event,

        /// <summary>
        /// Plugin publishes an event to the kernel for distribution.
        /// </summary>
        Publish,

        /// <summary>
        /// Plugin sends heartbeat to kernel (keep-alive).  
        /// </summary>
        Heartbeat,

        /// <summary>
        /// Kernel tells plugin to shutdown and exit.
        /// </summary>
        Shutdown,

        /// <summary>
        /// Plugin acknowledges a command from the kernel. 
        /// </summary>
        Ack,

        /// <summary>
        /// Plugin reports an error to the kernel.  
        /// </summary>
        Error
    }
}