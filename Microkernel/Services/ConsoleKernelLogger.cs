using System;

namespace Microkernel.Services
{
    public sealed class ConsoleKernelLogger : IKernelLogger
    {
        private readonly object _lock = new object();
        private static string _currentInput = "";
        private static int _cursorPos = 0;
        private static bool _isPromptActive = false;

        // New: mute flag - if true, plugin logs and forwarded subscriptions are suppressed
        private static bool _isMuted = false;

        public static void SetPromptState(bool active, string currentInput = "", int cursorPos = 0)
        {
            _isPromptActive = active;
            _currentInput = currentInput;
            _cursorPos = cursorPos;
        }

        public static void UpdateCurrentInput(string input, int cursorPos)
        {
            _currentInput = input;
            _cursorPos = cursorPos;
        }

        public static void SetMuted(bool muted)
        {
            _isMuted = muted;
        }

        public static bool IsMuted()
        {
            return _isMuted;
        }

        public void Debug(string message)
        {
            if (_isMuted) return;
            Log("DEBUG", message, ConsoleColor.Gray);
        }

        public void Info(string message)
        {
            if (_isMuted) return;
            Log("INFO ", message, ConsoleColor.White);
        }

        public void Warn(string message)
        {
            if (_isMuted) return;
            Log("WARN ", message, ConsoleColor.Yellow);
        }

        public void Error(string message)
        {
            if (_isMuted) return;
            Log("ERROR", message, ConsoleColor.Red);
        }

        private void Log(string level, string message, ConsoleColor color)
        {
            lock (_lock)
            {
                bool wasPromptActive = _isPromptActive;
                string savedInput = _currentInput;
                int savedCursorPos = _cursorPos;

                try
                {
                    if (wasPromptActive)
                    {
                        ClearCurrentLine();
                    }

                    string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");

                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write("[" + timestamp + "] ");

                    Console.ForegroundColor = color;
                    Console.Write("[" + level + "] ");

                    Console.ResetColor();
                    Console.WriteLine(message);

                    if (wasPromptActive)
                    {
                        Console.Write("> " + savedInput);
                        if (savedCursorPos < savedInput.Length)
                        {
                            int moveBack = savedInput.Length - savedCursorPos;
                            Console.Write(new string('\b', moveBack));
                        }
                    }
                }
                catch
                {
                    // Ignore console errors
                }
            }
        }

        private void ClearCurrentLine()
        {
            try
            {
                int totalLength = 2 + _currentInput.Length;
                Console.Write("\r");
                Console.Write(new string(' ', totalLength));
                Console.Write("\r");
            }
            catch
            {
                // Ignore errors
            }
        }
    }
}