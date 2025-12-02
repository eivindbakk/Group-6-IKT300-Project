using System;
using System.Collections.Generic;
using System. Linq;
using Microkernel.Core;

namespace Microkernel.Services
{
    public class AutocompleteService
    {
        private readonly Kernel _kernel;
        private readonly string[] _commands = { "help", "status", "plugins", "send", "exit", "quit", "list", "subscribe", "unsubscribe", "subscriptions", "filter", "unfilter", "filters", "mute", "unmute", "unload", "start", "stop", "crash", "restart" };

        public AutocompleteService(Kernel kernel)
        {
            _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        }

        public string TryAutocomplete(string input)
        {
            if (string. IsNullOrEmpty(input))
            {
                return input;
            }

            string[] parts = input. Split(new[] { ' ' }, StringSplitOptions. RemoveEmptyEntries);

            if (parts. Length == 0) return input;

            if (parts. Length == 1 && ! input.EndsWith(" "))
            {
                string match = _commands.FirstOrDefault(c =>
                    c.StartsWith(parts[0], StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    return match;
                }
            }

            string cmd = parts[0]. ToLowerInvariant();

            // Commands that take a plugin name as argument
            if (cmd == "help" || cmd == "unload" || cmd == "crash" || cmd == "restart" || cmd == "start" || cmd == "stop")
            {
                return AutocompletePluginName(input, parts, cmd);
            }

            if (cmd == "send")
            {
                return AutocompleteSend(input, parts);
            }

            if (cmd == "subscribe")
            {
                return AutocompleteSubscribe(input, parts);
            }

            if (cmd == "filter")
            {
                return AutocompleteFilter(input, parts);
            }

            return input;
        }

        private string AutocompletePluginName(string input, string[] parts, string command)
        {
            var plugins = _kernel.GetLoadedPlugins();

            if (parts.Length == 1 && input.EndsWith(" "))
            {
                if (plugins.Count > 0)
                {
                    return command + " " + plugins[0].Name;
                }
            }
            else if (parts.Length == 2 && ! input.EndsWith(" "))
            {
                var match = plugins.FirstOrDefault(p =>
                    p.Name.StartsWith(parts[1], StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    return command + " " + match.Name;
                }
            }

            return input;
        }

        private string AutocompleteSend(string input, string[] parts)
        {
            var allTopics = GetAllTopics();

            if (parts. Length == 1 && input.EndsWith(" "))
            {
                return "send metrics.";
            }
            else if (parts. Length == 2 && !input.EndsWith(" "))
            {
                var match = allTopics. FirstOrDefault(t =>
                    t.StartsWith(parts[1], StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    return "send " + match;
                }
            }

            return input;
        }

        private string AutocompleteSubscribe(string input, string[] parts)
        {
            var subTopics = new List<string> { "metrics.*", "alert.*", "*" };
            subTopics.AddRange(GetAllTopics());

            if (parts.Length == 1 && input. EndsWith(" "))
            {
                return "subscribe metrics.*";
            }
            else if (parts.Length == 2 && !input. EndsWith(" "))
            {
                var match = subTopics.FirstOrDefault(t =>
                    t.StartsWith(parts[1], StringComparison. OrdinalIgnoreCase));
                if (match != null)
                {
                    return "subscribe " + match;
                }
            }

            return input;
        }

        private string AutocompleteFilter(string input, string[] parts)
        {
            var filterPatterns = new List<string> { "metrics.*", "alert.*", "alert. critical", "alert.warning", "*" };

            if (parts. Length == 1 && input.EndsWith(" "))
            {
                return "filter metrics.*";
            }
            else if (parts.Length == 2 && !input.EndsWith(" "))
            {
                var match = filterPatterns.FirstOrDefault(t =>
                    t.StartsWith(parts[1], StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    return "filter " + match;
                }
            }

            return input;
        }

        private List<string> GetAllTopics()
        {
            var topics = new List<string>
            {
                "metrics.cpu",
                "metrics.memory",
                "metrics.system",
                "log",
                "alert. warning",
                "alert.critical"
            };

            foreach (var pluginInfo in _kernel.GetLoadedPlugins())
            {
                var plugin = _kernel. GetPlugin(pluginInfo.Name);
                if (plugin != null)
                {
                    var help = plugin.GetHelp();
                    foreach (var cmd in help.Commands)
                    {
                        if (! string.IsNullOrWhiteSpace(cmd. Topic) && !topics.Contains(cmd.Topic))
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