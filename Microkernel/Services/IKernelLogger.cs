namespace Microkernel. Services
{
    public interface IKernelLogger
    {
        void Debug(string message);
        void Info(string message);
        void Warn(string message);
        void Error(string message);
    }
}