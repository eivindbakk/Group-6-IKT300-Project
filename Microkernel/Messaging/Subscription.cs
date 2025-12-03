using System;
using Contracts;

namespace Microkernel.Messaging
{
    /// <summary>
    /// Represents an active subscription to the message bus.
    /// Implements IDisposable for automatic cleanup when disposed.
    /// </summary>
    internal sealed class Subscription : IDisposable
    {
        private readonly Action<Guid> _unsubscribe;
        private volatile bool _disposed;

        public Guid Id { get; }
        public string TopicPattern { get; }
        public Action<EventMessage> Handler { get; }

        public Subscription(string topicPattern, Action<EventMessage> handler, Action<Guid> unsubscribe)
        {
            Id = Guid.NewGuid();
            TopicPattern = topicPattern;
            Handler = handler ?? throw new ArgumentNullException(nameof(handler));
            _unsubscribe = unsubscribe ?? throw new ArgumentNullException(nameof(unsubscribe));
        }

        /// <summary>
        /// Checks if a topic matches this subscription's pattern. 
        /// Supports wildcard (*) matching.
        /// </summary>
        public bool Matches(string topic)
        {
            // Null or empty pattern matches everything
            if (string.IsNullOrEmpty(TopicPattern) || TopicPattern == "*")
            {
                return true;
            }

            // Wildcard at end: "metrics.*" matches "metrics.system", "metrics.cpu", etc.
            if (TopicPattern.EndsWith("*"))
            {
                var prefix = TopicPattern. TrimEnd('*');
                return topic != null && topic.StartsWith(prefix, StringComparison. OrdinalIgnoreCase);
            }

            // Exact match (case-insensitive)
            return string.Equals(TopicPattern, topic, StringComparison. OrdinalIgnoreCase);
        }

        /// <summary>
        /// Disposing the subscription automatically unsubscribes from the message bus.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _unsubscribe(Id);
        }
    }
}