namespace Contracts
{
    /// <summary>
    /// Interface the kernel exposes to plugins. 
    /// Plugins use this to communicate with the kernel.
    /// </summary>
    public interface IPluginHost
    {
        /// <summary>
        /// Publish an event to the kernel for routing to other plugins.
        /// </summary>
        /// <param name="evt">Event to publish.</param>
        void Publish(EventMessage evt);

        /// <summary>
        /// Log a message through the kernel's logging system.
        /// </summary>
        /// <param name="message">Message to log. </param>
        void Log(string message);
    }
}