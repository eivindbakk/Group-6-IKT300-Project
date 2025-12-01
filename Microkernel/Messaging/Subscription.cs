using System;
using Contracts;

namespace Microkernel. Messaging
{
    /// <summary>
    /// Represents an active subscription to the message bus.
    /// </summary>
    internal sealed class Subscription : IDisposable
    {
        private readonly Action _unsubscribe;
        private bool _disposed;

        public string TopicPattern { get; private set; }
        public Action<EventMessage> Handler { get; private set; }
        public Guid Id { get; private set; }

        public Subscription(string topicPattern, Action<EventMessage> handler, Action unsubscribe)
        {
            TopicPattern = topicPattern;
            Handler = handler ??  throw new ArgumentNullException(nameof(handler));
            _unsubscribe = unsubscribe ??  throw new ArgumentNullException(nameof(unsubscribe));
            Id = Guid. NewGuid();
        }

        public bool Matches(string topic)
        {
            if (string.IsNullOrEmpty(TopicPattern) || TopicPattern == "*")
            {
                return true;
            }

            // Simple wildcard matching
            if (TopicPattern. EndsWith("*"))
            {
                var prefix = TopicPattern. TrimEnd('*');
                return topic. StartsWith(prefix, StringComparison. OrdinalIgnoreCase);
            }

            return string.Equals(TopicPattern, topic, StringComparison. OrdinalIgnoreCase);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _unsubscribe();
            _disposed = true;
        }
    }
}