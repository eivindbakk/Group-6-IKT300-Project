using System;
using System.Collections.Generic;
using System.Linq;
using Contracts;
using Microkernel.Core;

namespace Microkernel.Services
{
    public class CommandHandler
    {
        private readonly IKernel _kernel;

        private readonly Dictionary<string, IDisposable> _subscriptions = new Dictionary<string, IDisposable>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _filters = new List<string>();

        public CommandHandler(IKernel kernel)
        {
            _kernel = kernel ??  throw new ArgumentNullException(nameof(kernel));
        }

        public bool ProcessCommand(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            string[] parts = input.Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
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
                            HelpRenderer.RenderGeneralHelp(_kernel. GetLoadedPlugins());
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
                            string payload = parts.Length > 2 ?  parts[2] : "";
                            SendEvent(topic, payload);
                        }
                        return false;

                    case "start":
                        if (parts.Length < 2)
                        {
                            Console.WriteLine("Usage: start <pluginname>");
                        }
                        else
                        {
                            StartPlugin(parts[1]);
                        }
                        return false;

                    case "stop":
                        if (parts. Length < 2)
                        {
                            Console.WriteLine("Usage: stop <pluginname>");
                        }
                        else
                        {
                            StopPlugin(parts[1]);
                        }
                        return false;

                    case "subscribe":
                        if (parts.Length < 2)
                        {
                            Console.WriteLine("Usage: subscribe <topicPattern>");
                        }
                        else
                        {
                            Subscribe(parts[1]);
                        }
                        return false;

                    case "unsubscribe":
                        if (parts.Length < 2)
                        {
                            Console.WriteLine("Usage: unsubscribe <topicPattern>");
                        }
                        else
                        {
                            Unsubscribe(parts[1]);
                        }
                        return false;

                    case "subscriptions":
                        ListSubscriptions();
                        return false;

                    case "filters":
                        ListFilters();
                        return false;

                    case "filter":
                        if (parts.Length < 2)
                        {
                            Console.WriteLine("Usage: filter <topicPattern>");
                        }
                        else
                        {
                            AddFilter(parts[1]);
                        }
                        return false;

                    case "unfilter":
                        if (parts.Length < 2)
                        {
                            Console.WriteLine("Usage: unfilter <topicPattern>");
                        }
                        else
                        {
                            RemoveFilter(parts[1]);
                        }
                        return false;

                    case "unload":
                        if (parts.Length < 2)
                        {
                            Console.WriteLine("Usage: unload <pluginName>");
                        }
                        else
                        {
                            UnloadPlugin(parts[1]);
                        }
                        return false;

                    case "mute":
                        ConsoleKernelLogger.SetMuted(true);
                        Console.WriteLine("Console output muted.");
                        return false;

                    case "unmute":
                        ConsoleKernelLogger.SetMuted(false);
                        Console. WriteLine("Console output unmuted.");
                        return false;

                    case "crash":
                        if (parts.Length < 2)
                        {
                            Console.WriteLine("Usage: crash <pluginName>");
                        }
                        else
                        {
                            CrashPlugin(parts[1]);
                        }
                        return false;

                    case "restart":
                        if (parts.Length < 2)
                        {
                            Console.WriteLine("Usage: restart <pluginName>");
                        }
                        else
                        {
                            RestartPlugin(parts[1]);
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

        private void StartPlugin(string pluginName)
        {
            var pluginInfo = FindPlugin(pluginName);
            if (pluginInfo == null) return;

            // Check if plugin is faulted
            if (pluginInfo. State == "Faulted")
            {
                Console.ForegroundColor = ConsoleColor. Yellow;
                Console. WriteLine("Plugin '" + pluginInfo.Name + "' is in FAULTED state.");
                Console.WriteLine("Use 'restart " + pluginInfo.Name + "' to recover it first.");
                Console. ResetColor();
                return;
            }

            // Send start event to the plugin
            string topic = GetPluginTopic(pluginInfo. Name, "start");

            var evt = new EventMessage
            {
                Topic = topic,
                Payload = "",
                Timestamp = DateTime. UtcNow,
                Source = "Console"
            };

            _kernel. Publish(evt);
            Console.WriteLine("Started: " + pluginInfo.Name);
        }

        private void StopPlugin(string pluginName)
        {
            var pluginInfo = FindPlugin(pluginName);
            if (pluginInfo == null) return;

            // Check if plugin is faulted
            if (pluginInfo.State == "Faulted")
            {
                Console.ForegroundColor = ConsoleColor. Yellow;
                Console.WriteLine("Plugin '" + pluginInfo.Name + "' is in FAULTED state.");
                Console.WriteLine("Use 'restart " + pluginInfo.Name + "' to recover it first.");
                Console.ResetColor();
                return;
            }

            // Send stop event to the plugin
            string topic = GetPluginTopic(pluginInfo.Name, "stop");

            var evt = new EventMessage
            {
                Topic = topic,
                Payload = "",
                Timestamp = DateTime.UtcNow,
                Source = "Console"
            };

            _kernel.Publish(evt);
            Console.WriteLine("Stopped: " + pluginInfo.Name);
        }

        private PluginInfo FindPlugin(string pluginName)
        {
            var plugins = _kernel.GetLoadedPlugins();
            
            var plugin = plugins.FirstOrDefault(p =>
                p.Name. Equals(pluginName, StringComparison.OrdinalIgnoreCase));

            if (plugin == null)
            {
                // Try partial match
                plugin = plugins.FirstOrDefault(p =>
                    p.Name. StartsWith(pluginName, StringComparison.OrdinalIgnoreCase));
            }

            // Handle shorthand names
            if (plugin == null && pluginName. Equals("generator", StringComparison.OrdinalIgnoreCase))
            {
                plugin = plugins.FirstOrDefault(p =>
                    p.Name. Equals("EventGenerator", StringComparison.OrdinalIgnoreCase));
            }

            if (plugin == null)
            {
                Console.WriteLine("Plugin not found: " + pluginName);
                if (plugins.Count > 0)
                {
                    Console.WriteLine("Available plugins:");
                    foreach (var p in plugins)
                    {
                        Console.WriteLine("  " + p. Name);
                    }
                }
                return null;
            }

            return plugin;
        }

        private string GetPluginTopic(string pluginName, string action)
        {
            // Handle known plugin topic patterns
            if (pluginName. Equals("EventGenerator", StringComparison.OrdinalIgnoreCase))
            {
                return "generator." + action;
            }

            return pluginName. ToLowerInvariant() + "." + action;
        }

        private void ShowPluginHelp(string pluginName)
        {
            var plugin = _kernel.GetPlugin(pluginName);

            if (plugin == null)
            {
                HelpRenderer.RenderPluginNotFound(pluginName, _kernel.GetLoadedPlugins());
                return;
            }

            HelpRenderer.RenderPluginHelp(plugin);
        }

        private void ShowStatus()
        {
            var plugins = _kernel. GetLoadedPlugins();

            Console.WriteLine();
            Console.WriteLine("  Kernel State:    " + (_kernel as Kernel)?.State);
            Console.WriteLine("  Active Plugins:  " + plugins.Count(p => p.State == "Running"));
            Console. WriteLine("  Faulted Plugins: " + plugins.Count(p => p. State == "Faulted"));
            Console.WriteLine("  Total Plugins:   " + plugins.Count);
            Console.WriteLine();
        }

        private void ListPlugins()
        {
            var plugins = _kernel.GetLoadedPlugins();

            if (plugins. Count == 0)
            {
                Console.WriteLine("No plugins loaded.");
                return;
            }

            Console.WriteLine();
            Console.WriteLine("Loaded Plugins (" + plugins.Count + "):");
            Console.WriteLine(new string('-', 60));
            Console.WriteLine(string.Format("{0,-20} {1,-10} {2,-12} {3}",
                "Name", "Version", "State", "Loaded At"));
            Console. WriteLine(new string('-', 60));

            foreach (var plugin in plugins)
            {
                if (plugin.State == "Faulted")
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                }
                else if (plugin.State == "Running")
                {
                    Console.ForegroundColor = ConsoleColor. Green;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor. Yellow;
                }

                Console.WriteLine(string. Format("{0,-20} {1,-10} {2,-12} {3:HH:mm:ss}",
                    plugin. Name, plugin.Version, plugin.State, plugin.LoadedAt));

                Console.ResetColor();
            }

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console. WriteLine("Tip: Type 'help <pluginname>' for plugin-specific help.");
            Console. WriteLine("Tip: Use 'start <pluginname>' or 'stop <pluginname>' to control plugins.");
            if (plugins.Any(p => p. State == "Faulted"))
            {
                Console. ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Tip: Use 'restart <pluginname>' to restart faulted plugins.");
            }
            Console.ResetColor();
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

        private void Subscribe(string pattern)
        {
            if (_subscriptions.ContainsKey(pattern))
            {
                Console.WriteLine("Already subscribed to: " + pattern);
                return;
            }

            try
            {
                IDisposable sub = _kernel.Subscribe(pattern, (evt) =>
                {
                    if (_filters.Count > 0)
                    {
                        bool anyMatch = _filters.Any(f => MatchesTopic(evt.Topic, f));
                        if (!anyMatch) return;
                    }

                    var msg = evt.Topic + ": " + evt. Payload;
                    Console.WriteLine(msg);
                });

                _subscriptions[pattern] = sub;
                Console. WriteLine("Subscribed to: " + pattern);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to subscribe: " + ex.Message);
            }
        }

        private void Unsubscribe(string pattern)
        {
            if (!_subscriptions.TryGetValue(pattern, out var sub))
            {
                Console.WriteLine("Not subscribed to: " + pattern);
                return;
            }

            try
            {
                sub.Dispose();
            }
            catch { }

            _subscriptions.Remove(pattern);
            Console. WriteLine("Unsubscribed from: " + pattern);
        }

        private void ListSubscriptions()
        {
            var kernelAuto = _kernel. GetAutoSubscriptions() ??  new List<string>();
            if (_subscriptions.Count == 0 && kernelAuto.Count == 0)
            {
                Console.WriteLine("No subscriptions.");
                return;
            }

            Console.WriteLine("Active subscriptions:");
            foreach (var p in kernelAuto)
            {
                Console.WriteLine("  (auto) " + p);
            }
            foreach (var p in _subscriptions. Keys)
            {
                Console.WriteLine("  " + p);
            }
        }

        private void AddFilter(string pattern)
        {
            if (_filters.Contains(pattern))
            {
                Console. WriteLine("Filter already present: " + pattern);
                return;
            }
            _filters.Add(pattern);
            Console.WriteLine("Added filter: " + pattern);
        }

        private void RemoveFilter(string pattern)
        {
            if (!_filters.Remove(pattern))
            {
                Console. WriteLine("Filter not found: " + pattern);
                return;
            }
            Console.WriteLine("Removed filter: " + pattern);
        }

        private void ListFilters()
        {
            if (_filters.Count == 0)
            {
                Console. WriteLine("No active filters.");
                return;
            }

            Console.WriteLine("Active filters:");
            foreach (var f in _filters)
            {
                Console.WriteLine("  " + f);
            }
        }

        private void UnloadPlugin(string pluginName)
        {
            var pluginInfo = FindPlugin(pluginName);
            if (pluginInfo == null) return;

            bool result = _kernel.UnloadPlugin(pluginInfo. Name);
            if (result)
            {
                Console. WriteLine("Plugin unloaded: " + pluginInfo. Name);
            }
            else
            {
                Console. WriteLine("Failed to unload plugin: " + pluginInfo.Name);
            }
        }

        private void CrashPlugin(string pluginName)
        {
            var pluginInfo = FindPlugin(pluginName);
            if (pluginInfo == null) return;

            if (pluginInfo. State == "Faulted")
            {
                Console. ForegroundColor = ConsoleColor.Yellow;
                Console. WriteLine("Plugin '" + pluginInfo. Name + "' is already in FAULTED state.");
                Console.ResetColor();
                return;
            }

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Crashing plugin: " + pluginInfo.Name);
            Console. ResetColor();

            bool result = _kernel. CrashPlugin(pluginInfo.Name);
            if (result)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console. WriteLine("Plugin " + pluginInfo. Name + " is now in FAULTED state.");
                Console. WriteLine("It will no longer receive events.");
                Console.WriteLine("Use 'restart " + pluginInfo.Name + "' to recover.");
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine("Failed to crash plugin: " + pluginInfo.Name);
            }
        }

        private void RestartPlugin(string pluginName)
        {
            var pluginInfo = FindPlugin(pluginName);
            if (pluginInfo == null) return;

            Console.WriteLine("Restarting plugin: " + pluginInfo. Name);

            bool result = _kernel.RestartPlugin(pluginInfo. Name);
            if (result)
            {
                Console. ForegroundColor = ConsoleColor.Green;
                Console. WriteLine("Plugin " + pluginInfo. Name + " restarted successfully.");
                Console.ResetColor();
            }
            else
            {
                Console. ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Failed to restart plugin: " + pluginInfo.Name);
                Console.ResetColor();
            }
        }

        private static bool MatchesTopic(string topic, string pattern)
        {
            if (string. Equals(topic, pattern, StringComparison.OrdinalIgnoreCase)) return true;
            if (pattern == "*") return true;
            if (! pattern.Contains("*")) return string.Equals(topic, pattern, StringComparison. OrdinalIgnoreCase);

            try
            {
                var escaped = System.Text.RegularExpressions. Regex.Escape(pattern). Replace("\\*", ".*");
                return System.Text.RegularExpressions. Regex.IsMatch(topic ??  "", "^" + escaped + "$", System.Text.RegularExpressions. RegexOptions. IgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }
}