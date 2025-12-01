using System;
using System.Collections.Generic;
using System.Linq;
using Microkernel.Core;

namespace Microkernel.Services
{
    /// <summary>
    /// Provides autocomplete functionality for the console.
    /// Follows Single Responsibility Principle - only handles autocomplete logic.
    /// </summary>
    public class AutocompleteService
    {
        private readonly Kernel _kernel;
        private readonly string[] _commands = { "help", "status", "plugins", "send", "exit", "quit", "list" };

        public AutocompleteService(Kernel kernel)
        {
            _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        }

        public string TryAutocomplete(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            string[] parts = input.Split(new[] { ' ' }, StringSplitOptions. RemoveEmptyEntries);

            // Command autocomplete
            if (parts.Length == 1 && ! input.EndsWith(" "))
            {
                string match = _commands.FirstOrDefault(c => 
                    c. StartsWith(parts[0], StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    return match;
                }
            }

            // Help + plugin name autocomplete
            if (parts.Length >= 1 && parts[0]. Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                return AutocompleteHelp(input, parts);
            }

            // Send + topic autocomplete
            if (parts.Length >= 1 && parts[0].Equals("send", StringComparison.OrdinalIgnoreCase))
            {
                return AutocompleteSend(input, parts);
            }

            return input;
        }

        private string AutocompleteHelp(string input, string[] parts)
        {
            var plugins = _kernel.GetLoadedPlugins();

            if (parts.Length == 1 && input.EndsWith(" "))
            {
                if (plugins.Count > 0)
                {
                    return "help " + plugins[0].Name;
                }
            }
            else if (parts.Length == 2)
            {
                var match = plugins.FirstOrDefault(p =>
                    p. Name.StartsWith(parts[1], StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    return "help " + match.Name;
                }
            }

            return input;
        }

        private string AutocompleteSend(string input, string[] parts)
        {
            // Get topics from all plugins
            var allTopics = GetAllTopics();

            if (parts. Length == 1 && input.EndsWith(" "))
            {
                return "send metrics.";
            }
            else if (parts. Length == 2 && ! input.EndsWith(" "))
            {
                var match = allTopics.FirstOrDefault(t =>
                    t. StartsWith(parts[1], StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    return "send " + match;
                }
            }

            return input;
        }

        private List<string> GetAllTopics()
        {
            var topics = new List<string>
            {
                // Default topics
                "metrics.cpu",
                "metrics.memory",
                "metrics.system",
                "log",
                "alert. warning",
                "alert.critical"
            };

            // Get topics from plugins
            foreach (var pluginInfo in _kernel.GetLoadedPlugins())
            {
                var plugin = _kernel. GetPlugin(pluginInfo.Name);
                if (plugin != null)
                {
                    var help = plugin.GetHelp();
                    foreach (var cmd in help.Commands)
                    {
                        if (! topics.Contains(cmd.Topic))
                        {
                            topics.Add(cmd.Topic);
                        }
                    }
                }
            }

            return topics;
        }
    }
}