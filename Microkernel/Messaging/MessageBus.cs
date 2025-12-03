using System;
using System. Collections. Concurrent;
using System.Collections.Generic;
using System. Linq;
using Contracts;
using Microkernel.Services;

namespace Microkernel.Messaging
{
    /// <summary>
    /// Thread-safe message bus for pub/sub communication. 
    /// Uses concurrent dictionary for thread-safe subscription management.
    /// </summary>
    public sealed class MessageBus : IMessageBus
    {
        private readonly IKernelLogger _logger;
        private readonly ConcurrentDictionary<Guid, Subscription> _subscriptions;

        public MessageBus(IKernelLogger logger)
        {
            _logger = logger ??  throw new ArgumentNullException(nameof(logger));
            _subscriptions = new ConcurrentDictionary<Guid, Subscription>();
        }

        /// <summary>
        /// Publishes a message to all matching subscribers.
        /// </summary>
        public void Publish(EventMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            // Take a snapshot of subscriptions (thread-safe iteration)
            var allSubscriptions = _subscriptions.Values.ToArray();

            var matchingSubscriptions = allSubscriptions
                . Where(s => s. Matches(message. Topic))
                . ToList();

            if (matchingSubscriptions.Count == 0)
            {
                return;
            }

            _logger.Debug(string.Format("Publishing message '{0}' to {1} subscriber(s)", 
                message.Topic, matchingSubscriptions. Count));

            // Invoke each subscriber's handler
            foreach (var subscription in matchingSubscriptions)
            {
                try
                {
                    subscription.Handler(message);
                }
                catch (Exception ex)
                {
                    // Log but don't crash - one bad subscriber shouldn't affect others
                    _logger.Error(string.Format("Subscriber threw exception: {0}", ex.Message));
                }
            }
        }

        /// <summary>
        /// Subscribes to messages matching a topic pattern. 
        /// Returns IDisposable - dispose to unsubscribe.
        /// </summary>
        public IDisposable Subscribe(string topicPattern, Action<EventMessage> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            var subscription = new Subscription(
                topicPattern,
                handler,
                id => Unsubscribe(id)
            );

            if (_subscriptions.TryAdd(subscription.Id, subscription))
            {
                _logger.Debug(string. Format("New subscription for pattern: {0}", topicPattern ??  "*"));
            }

            return subscription;
        }

        private void Unsubscribe(Guid subscriptionId)
        {
            if (_subscriptions.TryRemove(subscriptionId, out var removed))
            {
                _logger.Debug(string.Format("Subscription removed for pattern: {0}", removed.TopicPattern ?? "*"));
            }
        }
    }
}