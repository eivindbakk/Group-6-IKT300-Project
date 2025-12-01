namespace Microkernel. Services
{
    /// <summary>
    /// Logging abstraction for the kernel. 
    /// Allows pluggable logging implementations. 
    /// </summary>
    public interface IKernelLogger
    {
        void Debug(string message);
        void Info(string message);
        void Warn(string message);
        void Error(string message);
    }
}