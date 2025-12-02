using System;
using System.Collections.  Generic;
using Microkernel.Core;
using Microkernel.Services;

namespace Microkernel
{
    class Program
    {
        private static Kernel _kernel;
        private static CommandHandler _commandHandler;
        private static List<string> _commandHistory = new List<string>();
        private static int _historyIndex = -1;
        private static string _currentInput = "";
        private static int _cursorPosition = 0;

        static void Main(string[] args)
        {
            Console.Title = "Microkernel - IKT300";
            
            PrintBanner();

            try
            {
                var configuration = KernelConfiguration.CreateDefault();
                _kernel = Kernel.CreateDefault(configuration);
                _commandHandler = new CommandHandler(_kernel);

                _kernel. Start();

                Console.WriteLine();
                PrintHelp();
                Console.WriteLine();

                RunInputLoop();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Fatal error: " + ex. Message);
                Console.ResetColor();
            }
            finally
            {
                Console.WriteLine("Shutting down...");
                _kernel?. Dispose();
            }
        }

        private static void PrintBanner()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console. WriteLine(@"
  __  __ _                _  __                    _ 
 |  \/  (_) ___ _ __ ___ | |/ /___ _ __ _ __   ___| |
 | |\/| | |/ __| '__/ _ \| ' // _ \ '__| '_ \ / _ \ |
 | |  | | | (__| | | (_) | . \  __/ |  | | | |  __/ |
 |_|  |_|_|\___|_|  \___/|_|\_\___|_|  |_| |_|\___|_|
                                                      
            IKT300 Project - Group 6
");
            Console.ResetColor();
        }

        private static void PrintHelp()
        {
            Console.WriteLine("Available Commands:");
            Console. WriteLine("-------------------");
            Console.WriteLine("  help                  Show this help message");
            Console.WriteLine("  help <pluginname>     Show help for a specific plugin");
            Console.WriteLine("  status                Show kernel status");
            Console.WriteLine("  plugins               List all loaded plugins");
            Console.WriteLine("  start <pluginname>    Start a plugin");
            Console.WriteLine("  stop <pluginname>     Stop a plugin");
            Console.WriteLine("  send <topic> [data]   Publish an event");
            Console.WriteLine("  demo                  Run demo with UserLoggedInEvent and DataProcessedEvent");
            Console.WriteLine("  userlogin [name]      Send a UserLoggedInEvent");
            Console.WriteLine("  dataprocessed [n]     Send a DataProcessedEvent");
            Console.WriteLine("  debug on|off          Toggle debug output");
            Console. WriteLine("  mute / unmute         Mute/unmute console output");
            Console.WriteLine("  exit                  Stop kernel and exit");
            Console.WriteLine();
            Console.WriteLine("Keyboard:");
            Console.WriteLine("-------------------");
            Console. WriteLine("  Tab                   Autocomplete command or plugin name");
            Console. WriteLine("  Up/Down               Navigate command history");
        }

        private static void RunInputLoop()
        {
            while (true)
            {
                Console.Write("> ");
                string input = ReadLineWithHistory();

                if (string. IsNullOrWhiteSpace(input))
                    continue;

                // Add to history
                if (_commandHistory.Count == 0 || _commandHistory[_commandHistory.Count - 1] != input)
                {
                    _commandHistory. Add(input);
                }
                _historyIndex = _commandHistory. Count;

                try
                {
                    bool shouldExit = _commandHandler.ProcessCommand(input);
                    if (shouldExit)
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Error: " + ex.Message);
                    Console.ResetColor();
                }
            }
        }

        private static string ReadLineWithHistory()
        {
            _currentInput = "";
            _cursorPosition = 0;

            while (true)
            {
                ConsoleKeyInfo key = Console.ReadKey(true);

                switch (key.Key)
                {
                    case ConsoleKey.Enter:
                        Console.WriteLine();
                        return _currentInput;

                    case ConsoleKey. Backspace:
                        if (_cursorPosition > 0)
                        {
                            _currentInput = _currentInput. Remove(_cursorPosition - 1, 1);
                            _cursorPosition--;
                            RedrawInput();
                        }
                        break;

                    case ConsoleKey.Delete:
                        if (_cursorPosition < _currentInput.Length)
                        {
                            _currentInput = _currentInput.Remove(_cursorPosition, 1);
                            RedrawInput();
                        }
                        break;

                    case ConsoleKey. LeftArrow:
                        if (_cursorPosition > 0)
                        {
                            _cursorPosition--;
                            Console.CursorLeft--;
                        }
                        break;

                    case ConsoleKey.RightArrow:
                        if (_cursorPosition < _currentInput.Length)
                        {
                            _cursorPosition++;
                            Console.CursorLeft++;
                        }
                        break;

                    case ConsoleKey.UpArrow:
                        if (_historyIndex > 0)
                        {
                            _historyIndex--;
                            _currentInput = _commandHistory[_historyIndex];
                            _cursorPosition = _currentInput. Length;
                            RedrawInput();
                        }
                        break;

                    case ConsoleKey. DownArrow:
                        if (_historyIndex < _commandHistory.Count - 1)
                        {
                            _historyIndex++;
                            _currentInput = _commandHistory[_historyIndex];
                            _cursorPosition = _currentInput.Length;
                            RedrawInput();
                        }
                        else if (_historyIndex == _commandHistory.Count - 1)
                        {
                            _historyIndex = _commandHistory.Count;
                            _currentInput = "";
                            _cursorPosition = 0;
                            RedrawInput();
                        }
                        break;

                    case ConsoleKey.Tab:
                        HandleTabCompletion();
                        break;

                    case ConsoleKey.Home:
                        Console.CursorLeft -= _cursorPosition;
                        _cursorPosition = 0;
                        break;

                    case ConsoleKey. End:
                        Console.CursorLeft += (_currentInput.Length - _cursorPosition);
                        _cursorPosition = _currentInput.Length;
                        break;

                    case ConsoleKey.Escape:
                        _currentInput = "";
                        _cursorPosition = 0;
                        RedrawInput();
                        break;

                    default:
                        if (! char.IsControl(key.KeyChar))
                        {
                            _currentInput = _currentInput. Insert(_cursorPosition, key.KeyChar. ToString());
                            _cursorPosition++;
                            RedrawInput();
                        }
                        break;
                }
            }
        }

        private static void RedrawInput()
        {
            int promptLength = 2; // "> "
            Console.CursorLeft = promptLength;
            Console.Write(_currentInput + new string(' ', 20)); // Clear extra chars
            Console.CursorLeft = promptLength + _cursorPosition;
        }

        private static void HandleTabCompletion()
        {
            string[] commands = { 
                "help", "status", "plugins", "start", "stop", "send", 
                "subscribe", "unsubscribe", "subscriptions", "filter", "unfilter",
                "mute", "unmute", "unload", "crash", "restart", 
                "demo", "userlogin", "dataprocessed", "debug", "exit" 
            };

            string input = _currentInput. ToLowerInvariant();
            
            // Find matching commands
            var matches = new List<string>();
            foreach (var cmd in commands)
            {
                if (cmd.StartsWith(input))
                {
                    matches.Add(cmd);
                }
            }

            // Also try plugin names if input starts with start/stop/help
            if (input.StartsWith("start ") || input.StartsWith("stop ") || input.StartsWith("help "))
            {
                string prefix = input. Substring(0, input.IndexOf(' ') + 1);
                string partial = input.Substring(input.IndexOf(' ') + 1);
                
                var plugins = _kernel.GetLoadedPlugins();
                foreach (var plugin in plugins)
                {
                    if (plugin.Name. ToLowerInvariant().StartsWith(partial))
                    {
                        matches.Add(prefix + plugin.Name);
                    }
                }
            }

            if (matches.Count == 1)
            {
                _currentInput = matches[0];
                _cursorPosition = _currentInput.Length;
                RedrawInput();
            }
            else if (matches.Count > 1)
            {
                Console.WriteLine();
                foreach (var match in matches)
                {
                    Console.Write(match + "  ");
                }
                Console.WriteLine();
                Console.Write("> " + _currentInput);
            }
        }
    }
}