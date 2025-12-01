using System;
using System.Threading;

namespace Microkernel.Services
{
    public sealed class ConsoleKernelLogger : IKernelLogger
    {
        private readonly object _lock = new object();
        private static string _currentInput = "";
        private static int _cursorPos = 0;
        private static bool _isPromptActive = false;

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

        public void Debug(string message)
        {
            Log("DEBUG", message, ConsoleColor.Gray);
        }

        public void Info(string message)
        {
            Log("INFO ", message, ConsoleColor.White);
        }

        public void Warn(string message)
        {
            Log("WARN ", message, ConsoleColor. Yellow);
        }

        public void Error(string message)
        {
            Log("ERROR", message, ConsoleColor.Red);
        }

        private void Log(string level, string message, ConsoleColor color)
        {
            lock (_lock)
            {
                // Save current state
                bool wasPromptActive = _isPromptActive;
                string savedInput = _currentInput;
                int savedCursorPos = _cursorPos;

                try
                {
                    // If prompt is active, clear the current line first
                    if (wasPromptActive)
                    {
                        ClearCurrentLine();
                    }

                    // Write the log message
                    string timestamp = DateTime.Now. ToString("HH:mm:ss.fff");
                    
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write("[" + timestamp + "] ");
                    
                    Console.ForegroundColor = color;
                    Console.Write("[" + level + "] ");
                    
                    Console. ResetColor();
                    Console.WriteLine(message);

                    // Restore prompt if it was active
                    if (wasPromptActive)
                    {
                        Console.Write("> " + savedInput);
                        // Position cursor correctly
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
                // Calculate total length: "> " + input
                int totalLength = 2 + _currentInput.Length;
                
                // Move to start of line
                Console.Write("\r");
                
                // Clear the line
                Console. Write(new string(' ', totalLength));
                
                // Move back to start
                Console.Write("\r");
            }
            catch
            {
                // Ignore errors
            }
        }
    }
}