using System;
using System.Collections.Generic;
using Contracts;

namespace Microkernel.Services
{
    /// <summary>
    /// Responsible for rendering plugin help information to the console.
    /// Follows Single Responsibility Principle - only handles help display.
    /// </summary>
    public static class HelpRenderer
    {
        public static void RenderPluginHelp(IPlugin plugin)
        {
            if (plugin == null)
            {
                Console.WriteLine("Plugin not found.");
                return;
            }

            var help = plugin.GetHelp();

            // Header
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console. WriteLine("════════════════════════════════════════════════════════════");
            Console.WriteLine("  " + plugin.Name + " v" + plugin.Version);
            Console.WriteLine("════════════════════════════════════════════════════════════");
            Console.ResetColor();
            Console.WriteLine();

            // Description
            Console.ForegroundColor = ConsoleColor.White;
            Console. WriteLine("  " + plugin.Description);
            Console.ResetColor();
            Console.WriteLine();

            if (! string.IsNullOrEmpty(help. DetailedDescription))
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console. WriteLine("  " + help.DetailedDescription);
                Console. ResetColor();
                Console.WriteLine();
            }

            // Commands
            if (help.Commands. Count > 0)
            {
                Console.ForegroundColor = ConsoleColor. Yellow;
                Console.WriteLine("  Commands:");
                Console. ResetColor();

                foreach (var cmd in help.Commands)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    string cmdText = "    send " + cmd.Topic;
                    if (! string.IsNullOrEmpty(cmd. PayloadFormat))
                    {
                        cmdText += " " + cmd.PayloadFormat;
                    }
                    Console.Write(cmdText. PadRight(40));
                    Console.ResetColor();
                    Console.WriteLine("- " + cmd.Description);
                }
                Console.WriteLine();
            }

            // Handled Topics
            if (help.HandledTopics.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("  Handled Topics:");
                Console.ResetColor();

                foreach (var topic in help. HandledTopics)
                {
                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                    Console.WriteLine("    " + topic);
                }
                Console.ResetColor();
                Console.WriteLine();
            }

            // Notes
            if (help.Notes.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console. WriteLine("  Notes:");
                Console.ResetColor();

                foreach (var note in help.Notes)
                {
                    Console.ForegroundColor = ConsoleColor.Gray;
                    Console.WriteLine("    " + note);
                }
                Console. ResetColor();
                Console.WriteLine();
            }

            // Examples
            if (help.Examples. Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("  Examples:");
                Console.ResetColor();

                foreach (var example in help.Examples)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine("    " + example);
                }
                Console. ResetColor();
                Console.WriteLine();
            }
        }

        public static void RenderGeneralHelp()
        {
            Console.WriteLine(@"
Available Commands:
───────────────────
  help                  Show this help message
  help <pluginname>     Show help for a specific plugin (Tab to autocomplete)
  status                Show kernel status
  plugins               List all loaded plugins
  send <topic> [data]   Publish an event (Tab to autocomplete topics)
  exit                  Stop kernel and exit

Keyboard:
───────────────────
  Tab                   Autocomplete command or plugin name
  Up/Down               Navigate command history

Examples:
───────────────────
  help MetricsLogger
  help EventGenerator
  send metrics.cpu {""value"": 75}
  send generator.stop
");
        }

        public static void RenderPluginNotFound(string pluginName, IReadOnlyList<PluginInfo> availablePlugins)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console. WriteLine("Plugin '" + pluginName + "' not found.");
            Console. WriteLine();
            Console.WriteLine("Available plugins:");

            foreach (var p in availablePlugins)
            {
                Console.WriteLine("  " + p. Name);
            }
            Console.ResetColor();
        }
    }
}