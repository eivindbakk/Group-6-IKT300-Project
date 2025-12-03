namespace Microkernel. Services
{
    /// <summary>
    /// Logging interface used throughout the kernel.
    /// </summary>
    public interface IKernelLogger
    {
        void Debug(string message);
        void Info(string message);
        void Warn(string message);
        void Error(string message);
    }
}