namespace Microkernel. Plugins
{
    /// <summary>
    /// Plugin lifecycle states.
    /// </summary>
    public enum PluginState
    {
        /// <summary>Plugin assembly loaded but not started.</summary>
        Loaded,

        /// <summary>Plugin is being started.</summary>
        Starting,

        /// <summary>Plugin is running and can handle events.</summary>
        Running,

        /// <summary>Plugin is being stopped.</summary>
        Stopping,

        /// <summary>Plugin has been stopped. </summary>
        Stopped,

        /// <summary>Plugin encountered an error. </summary>
        Faulted
    }
}