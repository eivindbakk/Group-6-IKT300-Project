using System;

namespace Contracts
{
    /// <summary>
    /// Event message passed between kernel and plugins. 
    /// This is the actual event payload wrapped inside IpcMessage.
    /// </summary>
    public class EventMessage
    {
        /// <summary>
        /// Topic/type of the event for routing (e.g., "UserLoggedInEvent").
        /// </summary>
        public string Topic { get; set; }

        /// <summary>
        /// Event payload - typically JSON-serialized event data.
        /// </summary>
        public string Payload { get; set; }

        /// <summary>
        /// When the event was created. 
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Optional source identifier (which plugin created this event).
        /// </summary>
        public string Source { get; set; }
    }
}