using System;
using System.Collections.Generic;
using System. Linq;
using Contracts;
using Microkernel.Services;

namespace Microkernel.Messaging
{
    public sealed class MessageBus : IMessageBus
    {
        private readonly IKernelLogger _logger;
        private readonly List<Subscription> _subscriptions = new List<Subscription>();
        private readonly object _lock = new object();

        public MessageBus(IKernelLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Publish(EventMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            List<Subscription> matchingSubscriptions;
            lock (_lock)
            {
                matchingSubscriptions = _subscriptions
                    .Where(s => s.Matches(message.Topic))
                    .ToList();
            }

            if (matchingSubscriptions.Count == 0)
            {
                return;
            }

            _logger.Debug(string.Format("Publishing message '{0}' to {1} subscriber(s)", message.Topic, matchingSubscriptions.Count));

            foreach (var subscription in matchingSubscriptions)
            {
                try
                {
                    subscription.Handler(message);
                }
                catch (Exception ex)
                {
                    _logger.Error(string.Format("Subscriber threw exception: {0}", ex. Message));
                }
            }
        }

        public IDisposable Subscribe(string topicPattern, Action<EventMessage> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            Subscription subscription = null;
            subscription = new Subscription(topicPattern, handler, () => Unsubscribe(subscription));

            lock (_lock)
            {
                _subscriptions. Add(subscription);
            }

            _logger.Debug(string.Format("New subscription for pattern: {0}", topicPattern ??  "*"));
            return subscription;
        }

        private void Unsubscribe(Subscription subscription)
        {
            lock (_lock)
            {
                _subscriptions.Remove(subscription);
            }
            _logger.Debug(string.Format("Subscription removed for pattern: {0}", subscription.TopicPattern ??  "*"));
        }
    }
}