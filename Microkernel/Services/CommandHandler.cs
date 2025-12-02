using System;
using System.Collections. Generic;
using System.Linq;
using System.Text. Json;
using Contracts;
using Contracts.Events;
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
                return false;

            string[] parts = input. Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
            string command = parts[0]. ToLowerInvariant();

            try
            {
                switch (command)
                {
                    case "help":
                    case "? ":
                        if (parts.Length > 1)
                            ShowPluginHelp(parts[1]);
                        else
                            HelpRenderer.RenderGeneralHelp(_kernel. GetLoadedPlugins());
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
                            Console.WriteLine("Usage: send <topic> [payload]");
                        else
                        {
                            string topic = parts[1];
                            string payload = parts.Length > 2 ?  parts[2] : "";
                            SendEvent(topic, payload);
                        }
                        return false;

                    case "start":
                        if (parts.Length < 2)
                            Console.WriteLine("Usage: start <pluginname>");
                        else
                            StartPlugin(parts[1]);
                        return false;

                    case "stop":
                        if (parts.Length < 2)
                            Console.WriteLine("Usage: stop <pluginname>");
                        else
                            StopPlugin(parts[1]);
                        return false;

                    case "subscribe":
                        if (parts.Length < 2)
                            Console.WriteLine("Usage: subscribe <topicPattern>");
                        else
                            Subscribe(parts[1]);
                        return false;

                    case "unsubscribe":
                        if (parts.Length < 2)
                            Console.WriteLine("Usage: unsubscribe <topicPattern>");
                        else
                            Unsubscribe(parts[1]);
                        return false;

                    case "subscriptions":
                        ListSubscriptions();
                        return false;

                    case "filters":
                        ListFilters();
                        return false;

                    case "filter":
                        if (parts.Length < 2)
                            Console.WriteLine("Usage: filter <topicPattern>");
                        else
                            AddFilter(parts[1]);
                        return false;

                    case "unfilter":
                        if (parts.Length < 2)
                            Console.WriteLine("Usage: unfilter <topicPattern>");
                        else
                            RemoveFilter(parts[1]);
                        return false;

                    case "unload":
                        if (parts.Length < 2)
                            Console.WriteLine("Usage: unload <pluginName>");
                        else
                            UnloadPlugin(parts[1]);
                        return false;

                    case "mute":
                        ConsoleKernelLogger.SetMuted(true);
                        Console.WriteLine("Console output muted.");
                        return false;

                    case "unmute":
                        ConsoleKernelLogger.SetMuted(false);
                        Console.WriteLine("Console output unmuted.");
                        return false;

                    case "crash":
                        if (parts.Length < 2)
                            Console.WriteLine("Usage: crash <pluginName>");
                        else
                            CrashPlugin(parts[1]);
                        return false;

                    case "restart":
                        if (parts.Length < 2)
                            Console.WriteLine("Usage: restart <pluginName>");
                        else
                            RestartPlugin(parts[1]);
                        return false;

                    // REQUIRED BY ASSIGNMENT: UserLoggedInEvent
                    case "userloggedin":
                    case "userlogin":
                    case "login":
                        SendUserLoggedInEvent(parts. Length > 1 ? string.Join(" ", parts.Skip(1)) : null);
                        return false;

                    // REQUIRED BY ASSIGNMENT: DataProcessedEvent
                    case "dataprocessed":
                    case "processed":
                        SendDataProcessedEvent(parts.Length > 1 ? string. Join(" ", parts. Skip(1)) : null);
                        return false;

                    // Demo command
                    case "demo":
                        RunDemo();
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

        /// <summary>
        /// Sends a UserLoggedInEvent (REQUIRED by assignment).
        /// </summary>
        private void SendUserLoggedInEvent(string payload)
        {
            UserLoggedInEvent userEvent;

            if (! string.IsNullOrWhiteSpace(payload))
            {
                try
                {
                    userEvent = JsonSerializer.Deserialize<UserLoggedInEvent>(payload, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                catch
                {
                    userEvent = new UserLoggedInEvent
                    {
                        UserId = Guid.NewGuid().ToString(),
                        Username = payload,
                        IpAddress = "127.0.0. 1"
                    };
                }
            }
            else
            {
                var random = new Random();
                string[] users = { "alice", "bob", "charlie", "diana", "eve" };
                string[] ips = { "192.168.1. 100", "10.0.0. 50", "172.16.0. 25" };

                userEvent = new UserLoggedInEvent
                {
                    UserId = Guid.NewGuid().ToString(),
                    Username = users[random.Next(users.Length)],
                    IpAddress = ips[random.Next(ips.Length)]
                };
            }

            var evt = new EventMessage
            {
                Topic = "UserLoggedInEvent",  // EXACT topic name as required
                Payload = JsonSerializer.Serialize(userEvent, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                Timestamp = DateTime.UtcNow,
                Source = "CommandHandler"
            };

            _kernel. Publish(evt);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Published UserLoggedInEvent:");
            Console.ResetColor();
            Console.WriteLine($"  User: {userEvent. Username} (ID: {userEvent.UserId})");
            Console. WriteLine($"  IP: {userEvent. IpAddress}");
            Console.WriteLine($"  Session: {userEvent.SessionId}");
        }

        /// <summary>
        /// Sends a DataProcessedEvent (REQUIRED by assignment).
        /// </summary>
        private void SendDataProcessedEvent(string payload)
        {
            DataProcessedEvent dataEvent;

            if (!string. IsNullOrWhiteSpace(payload))
            {
                try
                {
                    dataEvent = JsonSerializer.Deserialize<DataProcessedEvent>(payload, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                catch
                {
                    int records = 0;
                    int. TryParse(payload, out records);
                    dataEvent = new DataProcessedEvent
                    {
                        DataSource = "CommandLine",
                        RecordsProcessed = records > 0 ? records : 100,
                        ProcessingTimeMs = new Random().NextDouble() * 1000
                    };
                }
            }
            else
            {
                var random = new Random();
                string[] sources = { "CustomerDB", "OrdersDB", "InventoryDB", "AnalyticsDB" };

                dataEvent = new DataProcessedEvent
                {
                    DataSource = sources[random.Next(sources.Length)],
                    RecordsProcessed = random.Next(50, 5000),
                    ProcessingTimeMs = random.NextDouble() * 2000,
                    Success = random.Next(10) != 0
                };

                if (! dataEvent.Success)
                    dataEvent.ErrorMessage = "Simulated processing error";
            }

            var evt = new EventMessage
            {
                Topic = "DataProcessedEvent",  // EXACT topic name as required
                Payload = JsonSerializer.Serialize(dataEvent, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                Timestamp = DateTime. UtcNow,
                Source = "CommandHandler"
            };

            _kernel.Publish(evt);

            Console. ForegroundColor = dataEvent.Success ? ConsoleColor.Green : ConsoleColor.Yellow;
            Console. WriteLine($"Published DataProcessedEvent:");
            Console.ResetColor();
            Console.WriteLine($"  Source: {dataEvent.DataSource}");
            Console.WriteLine($"  Records: {dataEvent.RecordsProcessed}");
            Console. WriteLine($"  Time: {dataEvent. ProcessingTimeMs:F2}ms");
            Console.WriteLine($"  Success: {dataEvent.Success}");
            if (!dataEvent.Success)
                Console.WriteLine($"  Error: {dataEvent.ErrorMessage}");
        }

        /// <summary>
        /// Runs demo with UserLoggedInEvent and DataProcessedEvent. 
        /// </summary>
        private void RunDemo()
        {
            Console. ForegroundColor = ConsoleColor. Cyan;
            Console.WriteLine("╔════════════════════════════════════════════════════════╗");
            Console.WriteLine("║     IKT300 Microkernel Demo - Required Events          ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();

            Console.WriteLine("1.  Sending UserLoggedInEvent...");
            SendUserLoggedInEvent(null);
            Console.WriteLine();

            System.Threading.Thread. Sleep(500);

            Console. WriteLine("2.  Sending DataProcessedEvent...");
            SendDataProcessedEvent(null);
            Console.WriteLine();

            System.Threading.Thread.Sleep(500);

            Console.WriteLine("3. Sending another UserLoggedInEvent...");
            SendUserLoggedInEvent(null);
            Console.WriteLine();

            System.Threading. Thread.Sleep(500);

            Console. WriteLine("4.  Sending another DataProcessedEvent.. .");
            SendDataProcessedEvent(null);
            Console. WriteLine();

            Console.ForegroundColor = ConsoleColor. Cyan;
            Console.WriteLine("Demo complete!  MetricsLogger should have logged all events.");
            Console.ResetColor();
        }

        // ...  rest of existing methods (StartPlugin, StopPlugin, etc.) remain the same ... 

        private void StartPlugin(string pluginName)
        {
            var pluginInfo = FindPlugin(pluginName);
            if (pluginInfo == null) return;

            if (pluginInfo.State == "Faulted")
            {
                Console. ForegroundColor = ConsoleColor.Yellow;
                Console. WriteLine($"Plugin '{pluginInfo.Name}' is FAULTED. Use 'restart {pluginInfo.Name}' first.");
                Console. ResetColor();
                return;
            }

            string topic = GetPluginTopic(pluginInfo. Name, "start");
            var evt = new EventMessage { Topic = topic, Payload = "", Timestamp = DateTime. UtcNow, Source = "Console" };
            _kernel. Publish(evt);
            Console.WriteLine("Started: " + pluginInfo.Name);
        }

        private void StopPlugin(string pluginName)
        {
            var pluginInfo = FindPlugin(pluginName);
            if (pluginInfo == null) return;

            if (pluginInfo.State == "Faulted")
            {
                Console.ForegroundColor = ConsoleColor. Yellow;
                Console.WriteLine($"Plugin '{pluginInfo. Name}' is FAULTED. Use 'restart {pluginInfo.Name}' first.");
                Console.ResetColor();
                return;
            }

            string topic = GetPluginTopic(pluginInfo.Name, "stop");
            var evt = new EventMessage { Topic = topic, Payload = "", Timestamp = DateTime. UtcNow, Source = "Console" };
            _kernel.Publish(evt);
            Console.WriteLine("Stopped: " + pluginInfo.Name);
        }

        private PluginInfo FindPlugin(string pluginName)
        {
            var plugins = _kernel.GetLoadedPlugins();
            var plugin = plugins.FirstOrDefault(p => p.Name. Equals(pluginName, StringComparison.OrdinalIgnoreCase));
            if (plugin == null)
                plugin = plugins.FirstOrDefault(p => p.Name.StartsWith(pluginName, StringComparison.OrdinalIgnoreCase));
            if (plugin == null && pluginName. Equals("generator", StringComparison.OrdinalIgnoreCase))
                plugin = plugins.FirstOrDefault(p => p.Name. Equals("EventGenerator", StringComparison.OrdinalIgnoreCase));

            if (plugin == null)
            {
                Console. WriteLine("Plugin not found: " + pluginName);
                if (plugins.Count > 0)
                {
                    Console. WriteLine("Available plugins:");
                    foreach (var p in plugins)
                        Console.WriteLine("  " + p. Name);
                }
            }
            return plugin;
        }

        private string GetPluginTopic(string pluginName, string action)
        {
            if (pluginName.Equals("EventGenerator", StringComparison.OrdinalIgnoreCase))
                return "generator." + action;
            return pluginName. ToLowerInvariant() + "." + action;
        }

        private void ShowPluginHelp(string pluginName)
        {
            var plugin = _kernel.GetPlugin(pluginName);
            if (plugin == null)
                HelpRenderer.RenderPluginNotFound(pluginName, _kernel.GetLoadedPlugins());
            else
                HelpRenderer.RenderPluginHelp(plugin);
        }

        private void ShowStatus()
        {
            var plugins = _kernel. GetLoadedPlugins();
            Console. WriteLine();
            Console.WriteLine($"  Kernel State:    {(_kernel as Kernel)?.State}");
            Console. WriteLine($"  Active Plugins:  {plugins.Count(p => p.State == "Running")}");
            Console.WriteLine($"  Faulted Plugins: {plugins.Count(p => p.State == "Faulted")}");
            Console.WriteLine($"  Total Plugins:   {plugins.Count}");
            Console. WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("  Required Event Commands:");
            Console. WriteLine("    userlogin [json|username]  - Send UserLoggedInEvent");
            Console. WriteLine("    dataprocessed [json|count] - Send DataProcessedEvent");
            Console.WriteLine("    demo                       - Run demo with both events");
            Console.ResetColor();
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
            Console.WriteLine($"Loaded Plugins ({plugins.Count}):");
            Console.WriteLine(new string('-', 60));
            Console.WriteLine($"{"Name",-20} {"Version",-10} {"State",-12} Loaded At");
            Console. WriteLine(new string('-', 60));

            foreach (var plugin in plugins)
            {
                Console. ForegroundColor = plugin.State == "Faulted" ?  ConsoleColor.Red :
                                         plugin.State == "Running" ? ConsoleColor.Green : ConsoleColor.Yellow;
                Console. WriteLine($"{plugin. Name,-20} {plugin.Version,-10} {plugin.State,-12} {plugin.LoadedAt:HH:mm:ss}");
                Console.ResetColor();
            }
            Console.WriteLine();
        }

        private void SendEvent(string topic, string payload)
        {
            var message = new EventMessage { Topic = topic, Payload = payload, Timestamp = DateTime. UtcNow };
            _kernel. Publish(message);
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
                    if (_filters.Count > 0 && ! _filters.Any(f => MatchesTopic(evt. Topic, f)))
                        return;
                    Console.WriteLine($"{evt.Topic}: {evt.Payload}");
                });
                _subscriptions[pattern] = sub;
                Console.WriteLine("Subscribed to: " + pattern);
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
            try { sub.Dispose(); } catch { }
            _subscriptions.Remove(pattern);
            Console.WriteLine("Unsubscribed from: " + pattern);
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
                Console.WriteLine("  (auto) " + p);
            foreach (var p in _subscriptions. Keys)
                Console.WriteLine("  " + p);
        }

        private void AddFilter(string pattern)
        {
            if (_filters.Contains(pattern))
            {
                Console. WriteLine("Filter already present: " + pattern);
                return;
            }
            _filters.Add(pattern);
            Console. WriteLine("Added filter: " + pattern);
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
                Console.WriteLine("No active filters.");
                return;
            }
            Console.WriteLine("Active filters:");
            foreach (var f in _filters)
                Console.WriteLine("  " + f);
        }

        private void UnloadPlugin(string pluginName)
        {
            var pluginInfo = FindPlugin(pluginName);
            if (pluginInfo == null) return;
            bool result = _kernel.UnloadPlugin(pluginInfo. Name);
            Console.WriteLine(result ?  $"Plugin unloaded: {pluginInfo.Name}" : $"Failed to unload plugin: {pluginInfo.Name}");
        }

        private void CrashPlugin(string pluginName)
        {
            var pluginInfo = FindPlugin(pluginName);
            if (pluginInfo == null) return;

            if (pluginInfo. State == "Faulted")
            {
                Console. ForegroundColor = ConsoleColor.Yellow;
                Console. WriteLine($"Plugin '{pluginInfo.Name}' is already FAULTED.");
                Console.ResetColor();
                return;
            }

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Crashing plugin: " + pluginInfo. Name);
            Console.ResetColor();

            bool result = _kernel. CrashPlugin(pluginInfo.Name);
            if (result)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console. WriteLine($"Plugin {pluginInfo.Name} is now FAULTED.");
                Console. WriteLine($"Use 'restart {pluginInfo.Name}' to recover.");
                Console. ResetColor();
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

            Console.WriteLine("Restarting plugin: " + pluginInfo.Name);
            bool result = _kernel. RestartPlugin(pluginInfo.Name);

            if (result)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Plugin {pluginInfo.Name} restarted successfully.");
                Console.ResetColor();
            }
            else
            {
                Console. ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Failed to restart plugin: {pluginInfo.Name}");
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
                return System.Text.RegularExpressions. Regex.IsMatch(topic ??  "", "^" + escaped + "$",
                    System. Text.RegularExpressions.RegexOptions.IgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }
}