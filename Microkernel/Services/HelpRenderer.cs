using System;
using System.Collections.Generic;
using System.Linq;
using Contracts;

namespace Microkernel.Services
{
    public static class HelpRenderer
    {
        public static void RenderGeneralHelp(IReadOnlyList<PluginInfo> plugins)
        {
            Console.WriteLine();
            Console.WriteLine("Available Commands:");
            Console. WriteLine(new string('-', 19));
            Console.WriteLine("  help                  Show this help message");
            Console.WriteLine("  help <pluginname>     Show help for a specific plugin");
            Console.WriteLine("  status                Show kernel status");
            Console.WriteLine("  plugins               List all loaded plugins");
            Console.WriteLine("  start <pluginname>    Start a plugin");
            Console.WriteLine("  stop <pluginname>     Stop a plugin");
            Console.WriteLine("  send <topic> [data]   Publish an event");
            Console.WriteLine("  subscribe <pattern>   Subscribe to topics");
            Console. WriteLine("  unsubscribe <pattern> Unsubscribe");
            Console. WriteLine("  subscriptions         List active subscriptions");
            Console. WriteLine("  filter <pattern>      Show only matching subscribed items");
            Console.WriteLine("  unfilter <pattern>    Remove filter");
            Console.WriteLine("  mute                  Mute console output");
            Console.WriteLine("  unmute                Unmute console output");
            Console.WriteLine("  unload <pluginname>   Stop and remove a plugin");
            Console.WriteLine("  exit                  Stop kernel and exit");
            Console.WriteLine();
            Console.WriteLine("Keyboard:");
            Console. WriteLine(new string('-', 19));
            Console.WriteLine("  Tab                   Autocomplete command or plugin name");
            Console.WriteLine("  Up/Down               Navigate command history");
            Console.WriteLine();
            Console. WriteLine("Examples:");
            Console.WriteLine(new string('-', 19));
            Console.WriteLine("  status");
            Console.WriteLine("  plugins");
            Console. WriteLine("  subscribe metrics.*");

            if (plugins != null && plugins.Count > 0)
            {
                Console.WriteLine("  help " + plugins[0]. Name);

                if (plugins.Any(p => p. Name.Equals("EventGenerator", StringComparison.OrdinalIgnoreCase)))
                {
                    Console.WriteLine("  start EventGenerator");
                }
            }

            Console.WriteLine();
        }

        public static void RenderPluginHelp(IPlugin plugin)
        {
            var help = plugin.GetHelp();

            Console.WriteLine();
            Console.WriteLine(new string('=', 60));
            Console. WriteLine("  " + plugin.Name + " v" + plugin.Version);
            Console. WriteLine(new string('=', 60));
            Console.WriteLine();
            Console.WriteLine("  " + plugin.Description);
            Console.WriteLine();

            if (! string.IsNullOrWhiteSpace(help. DetailedDescription))
            {
                Console.WriteLine("  " + help. DetailedDescription);
                Console.WriteLine();
            }

            if (help.Commands != null && help.Commands.Count > 0)
            {
                Console.WriteLine("  Commands:");
                int maxLen = help.Commands.Max(c => c.Topic.Length + (c.PayloadFormat ??  "").Length) + 4;
                foreach (var cmd in help. Commands)
                {
                    string cmdText = cmd.Topic;
                    if (! string.IsNullOrWhiteSpace(cmd.PayloadFormat))
                    {
                        cmdText += " " + cmd. PayloadFormat;
                    }
                    Console.WriteLine("    " + cmdText.PadRight(maxLen) + " - " + cmd.Description);
                }
                Console.WriteLine();
            }

            if (help.Notes != null && help. Notes.Count > 0)
            {
                Console.WriteLine("  Notes:");
                foreach (var note in help.Notes)
                {
                    Console.WriteLine("    " + note);
                }
                Console. WriteLine();
            }

            if (help.Examples != null && help. Examples.Count > 0)
            {
                Console.WriteLine("  Examples:");
                foreach (var example in help.Examples)
                {
                    Console.WriteLine("    " + example);
                }
                Console. WriteLine();
            }
        }

        public static void RenderPluginNotFound(string pluginName, IReadOnlyList<PluginInfo> plugins)
        {
            Console.WriteLine("Plugin not found: " + pluginName);

            if (plugins. Count > 0)
            {
                var closeMatch = plugins.FirstOrDefault(p =>
                    p. Name.StartsWith(pluginName, StringComparison.OrdinalIgnoreCase));

                if (closeMatch != null)
                {
                    Console.WriteLine("Did you mean: " + closeMatch. Name + "?");
                }
                else
                {
                    Console.WriteLine("Available plugins:");
                    foreach (var p in plugins)
                    {
                        Console.WriteLine("  " + p.Name);
                    }
                }
            }
        }
    }
}