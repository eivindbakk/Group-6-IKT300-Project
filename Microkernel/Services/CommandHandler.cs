using System;
using Contracts;
using Microkernel.Core;

namespace Microkernel.Services
{
    public class CommandHandler
    {
        private readonly Kernel _kernel;

        public CommandHandler(Kernel kernel)
        {
            _kernel = kernel ??  throw new ArgumentNullException(nameof(kernel));
        }

        public bool ProcessCommand(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            string[] parts = input. Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
            string command = parts[0]. ToLowerInvariant();

            try
            {
                switch (command)
                {
                    case "help":
                    case "? ":
                        if (parts.Length > 1)
                        {
                            ShowPluginHelp(parts[1]);
                        }
                        else
                        {
                            HelpRenderer.RenderGeneralHelp();
                        }
                        return false;

                    case "status":
                        ShowStatus();
                        return false;

                    case "plugins":
                    case "list":
                        ListPlugins();
                        return false;

                    case "send":
                    case "publish":
                        if (parts.Length < 2)
                        {
                            Console.WriteLine("Usage: send <topic> [payload]");
                        }
                        else
                        {
                            string topic = parts[1];
                            string payload = parts.Length > 2 ? parts[2] : "";
                            SendEvent(topic, payload);
                        }
                        return false;

                    case "exit":
                    case "quit":
                    case "q":
                        return true;

                    default:
                        Console.WriteLine("Unknown command: " + command + ". Type 'help' for commands.");
                        return false;
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error: " + ex.Message);
                Console.ResetColor();
                return false;
            }
        }

        private void ShowPluginHelp(string pluginName)
        {
            var plugin = _kernel.GetPlugin(pluginName);

            if (plugin == null)
            {
                HelpRenderer.RenderPluginNotFound(pluginName, _kernel. GetLoadedPlugins());
                return;
            }

            HelpRenderer.RenderPluginHelp(plugin);
        }

        private void ShowStatus()
        {
            var plugins = _kernel.GetLoadedPlugins();

            Console.WriteLine();
            Console.WriteLine("  Kernel State:   " + _kernel.State);
            Console.WriteLine("  Active Plugins: " + plugins.Count);
            Console.WriteLine();
        }

        private void ListPlugins()
        {
            var plugins = _kernel.GetLoadedPlugins();

            if (plugins.Count == 0)
            {
                Console. WriteLine("No plugins loaded.");
                return;
            }

            Console.WriteLine();
            Console.WriteLine("Loaded Plugins (" + plugins.Count + "):");
            Console.WriteLine(new string('─', 60));
            
            // Fix: Use string concatenation instead of string. Format to avoid ambiguity
            string header = "Name".PadRight(20) + "Version".PadRight(10) + "State".PadRight(12) + "Loaded At";
            Console.WriteLine(header);
            Console.WriteLine(new string('─', 60));

            foreach (var plugin in plugins)
            {
                // Fix: Use string concatenation instead of string. Format to avoid ambiguity
                string line = plugin.Name.PadRight(20) + 
                              plugin.Version.PadRight(10) + 
                              plugin.State.PadRight(12) + 
                              plugin.LoadedAt.ToString("HH:mm:ss");
                Console. WriteLine(line);
            }

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console. WriteLine("Tip: Type 'help <pluginname>' for plugin-specific help.");
            Console. ResetColor();
        }

        private void SendEvent(string topic, string payload)
        {
            var message = new EventMessage
            {
                Topic = topic,
                Payload = payload,
                Timestamp = DateTime.UtcNow
            };

            _kernel.Publish(message);
            Console.WriteLine("Event published.");
        }
    }
}