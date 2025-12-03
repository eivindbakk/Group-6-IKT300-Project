using System;

namespace Microkernel.Services
{
    /// <summary>
    /// Simple console logger with timestamp and log level formatting.
    /// Supports debug mode toggle and output muting.
    /// </summary>
    public class ConsoleKernelLogger : IKernelLogger
    {
        private static bool _debugEnabled = false;
        private static bool _muted = false;
        
        // Optional custom output handler (used by Program.cs for clean console output)
        private static Action<string> _outputHandler = null;

        public static void SetOutputHandler(Action<string> handler)
        {
            _outputHandler = handler;
        }

        public static void EnableDebug()
        {
            _debugEnabled = true;
        }

        public static void DisableDebug()
        {
            _debugEnabled = false;
        }

        public static void SetMuted(bool muted)
        {
            _muted = muted;
        }

        private void Output(string message)
        {
            if (_muted) return;

            if (_outputHandler != null)
            {
                _outputHandler(message);
            }
            else
            {
                Console. WriteLine(message);
            }
        }

        public void Info(string message)
        {
            string line = "[" + DateTime.Now. ToString("HH:mm:ss.fff") + "] [INFO ] " + message;
            Output(line);
        }

        public void Warn(string message)
        {
            string line = "[" + DateTime.Now. ToString("HH:mm:ss.fff") + "] [WARN ] " + message;
            Output(line);
        }

        public void Error(string message)
        {
            string line = "[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] [ERROR] " + message;
            Output(line);
        }

        public void Debug(string message)
        {
            // Debug messages only shown when debug mode is enabled
            if (! _debugEnabled) return;
            string line = "[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] [DEBUG] " + message;
            Output(line);
        }
    }
}