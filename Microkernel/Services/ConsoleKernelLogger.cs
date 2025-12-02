using System;

namespace Microkernel.Services
{
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warn = 2,
        Error = 3
    }

    public class ConsoleKernelLogger : IKernelLogger
    {
        private static bool _muted = false;
        private static LogLevel _minLevel = LogLevel. Info;
        private static readonly object _lock = new object();

        public static void SetMuted(bool muted)
        {
            _muted = muted;
        }

        public static void EnableDebug()
        {
            _minLevel = LogLevel. Debug;
        }

        public static void DisableDebug()
        {
            _minLevel = LogLevel. Info;
        }

        public void Debug(string message)
        {
            if (_minLevel <= LogLevel. Debug)
            {
                Log("DEBUG", message, ConsoleColor.DarkGray);
            }
        }

        public void Info(string message)
        {
            if (_minLevel <= LogLevel.Info)
            {
                Log("INFO ", message, ConsoleColor.White);
            }
        }

        public void Warn(string message)
        {
            if (_minLevel <= LogLevel. Warn)
            {
                Log("WARN ", message, ConsoleColor.Yellow);
            }
        }

        public void Error(string message)
        {
            if (_minLevel <= LogLevel.Error)
            {
                Log("ERROR", message, ConsoleColor.Red);
            }
        }

        private void Log(string level, string message, ConsoleColor color)
        {
            if (_muted) return;

            lock (_lock)
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write("[" + timestamp + "] ");
                Console. ForegroundColor = color;
                Console.Write("[" + level + "] ");
                Console.ResetColor();
                Console.WriteLine(message);
            }
        }
    }
}