using System;
using System. Collections.Generic;
using System.IO;
using System. Linq;
using System.  Threading;
using Microkernel.Core;
using Microkernel.Services;

namespace Microkernel
{
    class Program
    {
        private static Kernel _kernel;
        private static CommandHandler _commandHandler;
        private static string _currentInput = "";
        private static int _cursorPosition = 0;
        private static List<string> _commandHistory = new List<string>();
        private static int _historyIndex = 0;
        private static readonly object _consoleLock = new object();
        private static int _inputLineRow = 0;
        private static bool _running = true;

        private static readonly string[] Commands = {
            "help", "status", "plugins", "demo", "userlogin", "dataprocessed",
            "metrics", "send", "generate", "load", "unload", "crash", "restart",
            "debug", "mute", "unmute", "exit"
        };

        static void Main(string[] args)
        {
            Console.Title = "Microkernel - IKT300 Project";
            Console. Clear();

            PrintBanner();

            // Create kernel and command handler
            _kernel = Kernel. CreateDefault();
            _commandHandler = new CommandHandler(_kernel);

            // Start kernel (this loads plugins)
            _kernel. Start();

            // Wait for plugins to connect
            Thread.Sleep(1000);

            // Now print help and set up input
            Console.WriteLine();
            PrintStartupInfo();

            // Hook up output redirection AFTER startup info is printed
            ConsoleKernelLogger.SetOutputHandler(WriteOutput);

            _inputLineRow = Console.CursorTop;
            RedrawInputLine();

            RunInputLoop();

            _kernel.Stop();
        }

        private static void PrintBanner()
        {
            Console.ForegroundColor = ConsoleColor. Cyan;
            Console.WriteLine(@"
  __  __ _                _  __                    _
 |  \/  (_) ___ _ __ ___ | |/ /___ _ __ _ __   ___| |
 | |\/| | |/ __| '__/ _ \| ' // _ \ '__| '_ \ / _ \ |
 | |  | | | (__| | | (_) | . \  __/ |  | | | |  __/ |
 |_|  |_|_|\___|_|  \___/|_|\_\___|_|  |_| |_|\___|_|

            IKT300 Project - Group 6
");
            Console.ResetColor();
            Console.WriteLine();
        }

        private static void PrintStartupInfo()
        {
            Console. ForegroundColor = ConsoleColor.White;
            Console. WriteLine("Available Commands:");
            Console.WriteLine("-------------------");
            Console.ResetColor();
            Console.WriteLine("  help                  Show this help message");
            Console.WriteLine("  status                Show kernel status");
            Console.WriteLine("  plugins               List all loaded plugins");
            Console. WriteLine("  demo                  Run demo with required events");
            Console.WriteLine("  userlogin [name]      Send a UserLoggedInEvent");
            Console.WriteLine("  dataprocessed [n]     Send a DataProcessedEvent");
            Console.WriteLine("  send <topic> [data]   Publish a custom event");
            Console. WriteLine("  load <path>           Load a plugin executable");
            Console.WriteLine("  unload <plugin>       Unload a plugin");
            Console.WriteLine("  crash <plugin>        Kill a plugin (fault isolation test)");
            Console.WriteLine("  restart <plugin>      Restart a plugin");
            Console.WriteLine("  debug on|off          Toggle debug output");
            Console. WriteLine("  mute / unmute         Mute/unmute console output");
            Console.WriteLine("  exit                  Stop kernel and exit");
            Console.WriteLine();
        }

        public static void WriteOutput(string message)
        {
            lock (_consoleLock)
            {
                string savedInput = _currentInput;
                int savedCursor = _cursorPosition;

                ClearInputLine();

                Console.SetCursorPosition(0, _inputLineRow);
                Console.WriteLine(message);

                _inputLineRow = Console. CursorTop;

                _currentInput = savedInput;
                _cursorPosition = savedCursor;
                RedrawInputLine();
            }
        }

        private static void ClearInputLine()
        {
            try
            {
                Console.SetCursorPosition(0, _inputLineRow);
                Console.Write(new string(' ', Console.WindowWidth - 1));
                Console.SetCursorPosition(0, _inputLineRow);
            }
            catch { }
        }

        private static void RedrawInputLine()
        {
            try
            {
                Console.SetCursorPosition(0, _inputLineRow);
                Console.Write("> " + _currentInput + new string(' ', Math.Max(0, Console.WindowWidth - _currentInput.Length - 3)));
                Console.SetCursorPosition(2 + _cursorPosition, _inputLineRow);
            }
            catch { }
        }

        private static void RunInputLoop()
        {
            while (_running)
            {
                try
                {
                    if (! Console.KeyAvailable)
                    {
                        Thread.Sleep(10);
                        continue;
                    }

                    var key = Console.ReadKey(true);

                    lock (_consoleLock)
                    {
                        HandleKeyPress(key);
                    }
                }
                catch (Exception)
                {
                    break;
                }
            }
        }

        private static void HandleKeyPress(ConsoleKeyInfo key)
        {
            switch (key.Key)
            {
                case ConsoleKey. Enter:
                    HandleEnter();
                    break;

                case ConsoleKey.Backspace:
                    HandleBackspace();
                    break;

                case ConsoleKey.Delete:
                    HandleDelete();
                    break;

                case ConsoleKey. LeftArrow:
                    if (_cursorPosition > 0)
                    {
                        _cursorPosition--;
                        RedrawInputLine();
                    }
                    break;

                case ConsoleKey.RightArrow:
                    if (_cursorPosition < _currentInput.Length)
                    {
                        _cursorPosition++;
                        RedrawInputLine();
                    }
                    break;

                case ConsoleKey. UpArrow:
                    HandleUpArrow();
                    break;

                case ConsoleKey.DownArrow:
                    HandleDownArrow();
                    break;

                case ConsoleKey.Tab:
                    HandleTabCompletion();
                    break;

                case ConsoleKey. Home:
                    _cursorPosition = 0;
                    RedrawInputLine();
                    break;

                case ConsoleKey.End:
                    _cursorPosition = _currentInput.Length;
                    RedrawInputLine();
                    break;

                case ConsoleKey.Escape:
                    _currentInput = "";
                    _cursorPosition = 0;
                    RedrawInputLine();
                    break;

                default:
                    if (! char.IsControl(key.KeyChar))
                    {
                        _currentInput = _currentInput.Insert(_cursorPosition, key.KeyChar. ToString());
                        _cursorPosition++;
                        RedrawInputLine();
                    }
                    break;
            }
        }

        private static void HandleEnter()
        {
            string input = _currentInput. Trim();
            _currentInput = "";
            _cursorPosition = 0;

            ClearInputLine();
            _inputLineRow++;

            if (_inputLineRow >= Console. BufferHeight - 1)
            {
                _inputLineRow = Console.BufferHeight - 2;
            }

            if (! string.IsNullOrEmpty(input))
            {
                _commandHistory.Add(input);
                _historyIndex = _commandHistory.Count;

                bool shouldExit = _commandHandler.ProcessCommand(input);
                if (shouldExit)
                {
                    _running = false;
                    return;
                }
            }

            _inputLineRow = Console.CursorTop;
            RedrawInputLine();
        }

        private static void HandleBackspace()
        {
            if (_cursorPosition > 0)
            {
                _currentInput = _currentInput.Remove(_cursorPosition - 1, 1);
                _cursorPosition--;
                RedrawInputLine();
            }
        }

        private static void HandleDelete()
        {
            if (_cursorPosition < _currentInput.Length)
            {
                _currentInput = _currentInput.Remove(_cursorPosition, 1);
                RedrawInputLine();
            }
        }

        private static void HandleUpArrow()
        {
            if (_historyIndex > 0)
            {
                _historyIndex--;
                _currentInput = _commandHistory[_historyIndex];
                _cursorPosition = _currentInput.Length;
                RedrawInputLine();
            }
        }

        private static void HandleDownArrow()
        {
            if (_historyIndex < _commandHistory.Count - 1)
            {
                _historyIndex++;
                _currentInput = _commandHistory[_historyIndex];
                _cursorPosition = _currentInput.Length;
                RedrawInputLine();
            }
            else if (_historyIndex == _commandHistory.Count - 1)
            {
                _historyIndex = _commandHistory.Count;
                _currentInput = "";
                _cursorPosition = 0;
                RedrawInputLine();
            }
        }

        private static void HandleTabCompletion()
        {
            string input = _currentInput. ToLowerInvariant(). TrimStart();
            var matches = new List<string>();

            string[] parts = input. Split(' ', 2);
            string command = parts[0];
            string partialArg = parts. Length > 1 ? parts[1] : null;

            if (partialArg != null)
            {
                if (command == "crash" || command == "restart" || command == "unload")
                {
                    var plugins = _kernel.GetLoadedPlugins();
                    foreach (var plugin in plugins)
                    {
                        string name = plugin.Name. ToLowerInvariant();
                        if (name.Contains(partialArg) || partialArg == "")
                        {
                            matches.Add(command + " " + plugin.Name);
                        }
                    }
                }
                else if (command == "debug")
                {
                    if ("on". StartsWith(partialArg)) matches. Add("debug on");
                    if ("off".StartsWith(partialArg)) matches.Add("debug off");
                }
                else if (command == "generate")
                {
                    if ("start". StartsWith(partialArg)) matches. Add("generate start");
                    if ("stop".StartsWith(partialArg)) matches.Add("generate stop");
                    if ("toggle".StartsWith(partialArg)) matches.Add("generate toggle");
                }
            }
            else
            {
                foreach (var cmd in Commands)
                {
                    if (cmd. StartsWith(command))
                    {
                        matches.Add(cmd);
                    }
                }
            }

            if (matches.Count == 1)
            {
                _currentInput = matches[0];
                _cursorPosition = _currentInput.Length;
                RedrawInputLine();
            }
            else if (matches.Count > 1)
            {
                WriteOutput(string.Join("  ", matches));
            }
        }
    }
}