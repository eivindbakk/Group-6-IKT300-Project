using System;
using Contracts;

namespace Microkernel. Messaging
{
    /// <summary>
    /// Message bus interface for pub/sub communication.
    /// </summary>
    public interface IMessageBus
    {
        /// <summary>
        /// Publish a message to all subscribers. 
        /// </summary>
        void Publish(EventMessage message);

        /// <summary>
        /// Subscribe to messages matching a topic pattern.
        /// </summary>
        /// <param name="topicPattern">Topic pattern (supports wildcards: * for any)</param>
        /// <param name="handler">Handler to invoke when message matches</param>
        /// <returns>Subscription that can be disposed to unsubscribe</returns>
        IDisposable Subscribe(string topicPattern, Action<EventMessage> handler);

        /// <summary>
        /// Subscribe to all messages.
        /// </summary>
        IDisposable SubscribeAll(Action<EventMessage> handler);
    }
}