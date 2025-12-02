using System;
using System. Collections.Generic;
using System.Threading;
using Microkernel.Core;
using Microkernel.Services;

namespace Microkernel
{
    /// <summary>
    /// Main program entry point.
    /// Responsibility: Console UI only - input/output handling. 
    /// </summary>
    class Program
    {
        private static Kernel _kernel;
        private static CommandHandler _commandHandler;
        private static AutocompleteService _autocomplete;
        private static readonly ManualResetEvent _exitEvent = new ManualResetEvent(false);
        private static readonly List<string> _commandHistory = new List<string>();
        private static int _historyIndex = -1;

        static void Main(string[] args)
        {
            Console.Title = "Microkernel";
            PrintBanner();

            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                _exitEvent.Set();
            };

            try
            {
                InitializeKernel();
                RunInteractiveLoop();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Fatal error: " + ex.Message);
                Console.ResetColor();
                Environment.ExitCode = 1;
            }
            finally
            {
                Shutdown();
            }
        }

        private static void InitializeKernel()
        {
            var config = KernelConfiguration.CreateDefault();
            _kernel = Kernel.CreateDefault(config);
            _commandHandler = new CommandHandler(_kernel);
            _autocomplete = new AutocompleteService(_kernel);
            
            _kernel.Start();

            Console.WriteLine();
            HelpRenderer.RenderGeneralHelp(_kernel. GetLoadedPlugins());
        }

        private static void Shutdown()
        {
            Console.WriteLine("\nShutting down.. .");
            if (_kernel != null)
            {
                _kernel.Stop();
                _kernel.Dispose();
            }
        }

        private static void RunInteractiveLoop()
        {
            while (!_exitEvent.WaitOne(0))
            {
                Console.Write("\n> ");
                ConsoleKernelLogger.SetPromptState(true, "", 0);

                string input = ReadLineWithAutocomplete();

                ConsoleKernelLogger.SetPromptState(false);

                if (input == null || _exitEvent.WaitOne(0))
                {
                    break;
                }

                input = input.Trim();
                if (! string.IsNullOrEmpty(input))
                {
                    _commandHistory.Add(input);
                    _historyIndex = _commandHistory.Count;

                    bool shouldExit = _commandHandler.ProcessCommand(input);
                    if (shouldExit)
                    {
                        break;
                    }
                }
            }
        }

        private static string ReadLineWithAutocomplete()
        {
            string input = "";
            int cursorPos = 0;

            while (true)
            {
                ConsoleKernelLogger.UpdateCurrentInput(input, cursorPos);

                if (! Console.KeyAvailable)
                {
                    Thread.Sleep(10);
                    if (_exitEvent.WaitOne(0)) return null;
                    continue;
                }

                ConsoleKeyInfo key = Console.ReadKey(true);

                switch (key.Key)
                {
                    case ConsoleKey.Enter:
                        Console.WriteLine();
                        return input;

                    case ConsoleKey. Tab:
                        string completed = _autocomplete.TryAutocomplete(input);
                        if (completed != input)
                        {
                            ClearInput(input.Length);
                            input = completed;
                            cursorPos = input. Length;
                            Console.Write(input);
                        }
                        break;

                    case ConsoleKey.Backspace:
                        if (cursorPos > 0)
                        {
                            input = input. Substring(0, cursorPos - 1) + input. Substring(cursorPos);
                            cursorPos--;
                            Console.Write("\b \b");
                        }
                        break;

                    case ConsoleKey.UpArrow:
                        if (_historyIndex > 0)
                        {
                            _historyIndex--;
                            ClearInput(input. Length);
                            input = _commandHistory[_historyIndex];
                            cursorPos = input. Length;
                            Console.Write(input);
                        }
                        break;

                    case ConsoleKey.DownArrow:
                        if (_historyIndex < _commandHistory.Count - 1)
                        {
                            _historyIndex++;
                            ClearInput(input.Length);
                            input = _commandHistory[_historyIndex];
                            cursorPos = input.Length;
                            Console.Write(input);
                        }
                        else if (_historyIndex == _commandHistory.Count - 1)
                        {
                            _historyIndex = _commandHistory.Count;
                            ClearInput(input.Length);
                            input = "";
                            cursorPos = 0;
                        }
                        break;

                    case ConsoleKey.Escape:
                        return null;

                    default:
                        if (! char.IsControl(key.KeyChar))
                        {
                            input = input.Substring(0, cursorPos) + key.KeyChar + input.Substring(cursorPos);
                            Console. Write(key.KeyChar);
                            cursorPos++;
                        }
                        break;
                }
            }
        }

        private static void ClearInput(int length)
        {
            if (length > 0)
            {
                Console.Write(new string('\b', length));
                Console.Write(new string(' ', length));
                Console.Write(new string('\b', length));
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
    }
}