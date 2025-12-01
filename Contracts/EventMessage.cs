using System;

namespace Contracts
{
    /// <summary>
    /// Event message passed between kernel and plugins.
    /// </summary>
    public class EventMessage
    {
        /// <summary>
        /// Topic/type of the event for routing.
        /// </summary>
        public string Topic { get; set; }

        /// <summary>
        /// Event payload (typically JSON). 
        /// </summary>
        public string Payload { get; set; }

        /// <summary>
        /// When the event was created.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Optional correlation ID for tracking related events.
        /// </summary>
        public string CorrelationId { get; set; }

        /// <summary>
        /// Optional source identifier. 
        /// </summary>
        public string Source { get; set; }
    }
}