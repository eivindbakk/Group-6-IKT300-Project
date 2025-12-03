namespace Microkernel. Core
{
    /// <summary>
    /// Represents the lifecycle state of the kernel. 
    /// </summary>
    public enum KernelState
    {
        Created,   // Just instantiated, not yet started
        Starting,  // Currently starting up
        Running,   // Fully operational
        Stopping,  // Shutting down gracefully
        Stopped,   // Shutdown complete
        Faulted    // Error state
    }
}