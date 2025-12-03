using System;
using System.IO;
using System. Linq;
using System.Text. Json;
using System. Threading;
using Contracts;
using Contracts.Events;
using Microkernel.Core;

namespace Microkernel.Services
{
    public class CommandHandler
    {
        private readonly IKernel _kernel;
        private readonly Random _random = new Random();
        private static bool _muted = false;
        private static bool _generating = false;

        public CommandHandler(IKernel kernel)
        {
            _kernel = kernel ??  throw new ArgumentNullException(nameof(kernel));
        }

        private void Print(string message)
        {
            if (! _muted)
                Console.WriteLine(message);
        }

        private void PrintColor(string message, ConsoleColor color)
        {
            if (_muted) return;
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console. ResetColor();
        }

        public bool ProcessCommand(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            string[] parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            string command = parts[0]. ToLowerInvariant();
            string args = parts.Length > 1 ? parts[1] : "";

            switch (command)
            {
                case "help":
                    ShowHelp(args);
                    return false;

                case "status":
                    ShowStatus();
                    return false;

                case "plugins":
                    ListPlugins();
                    return false;

                case "demo":
                    RunDemo();
                    return false;

                case "userlogin":
                    SendUserLoggedInEvent(args);
                    return false;

                case "dataprocessed":
                    SendDataProcessedEvent(args);
                    return false;

                case "metrics":
                    SendSystemMetricsEvent();
                    return false;

                case "send":
                    SendCustomEvent(args);
                    return false;

                case "generate":
                    HandleGenerate(args);
                    return false;

                case "load":
                    LoadPlugin(args);
                    return false;

                case "unload":
                    UnloadPlugin(args);
                    return false;

                case "crash":
                    CrashPlugin(args);
                    return false;

                case "restart":
                    RestartPlugin(args);
                    return false;

                case "debug":
                    ToggleDebug(args);
                    return false;

                case "mute":
                    _muted = true;
                    ConsoleKernelLogger.SetMuted(true);
                    Console.WriteLine("Output muted.");
                    return false;

                case "unmute":
                    _muted = false;
                    ConsoleKernelLogger.SetMuted(false);
                    Console. WriteLine("Output unmuted.");
                    return false;

                case "exit":
                case "quit":
                    return true;

                default:
                    Print("Unknown command.  Type 'help' for available commands.");
                    return false;
            }
        }

        private void ShowHelp(string args)
        {
            string topic = args.ToLowerInvariant(). Trim();

            if (string.IsNullOrEmpty(topic))
            {
                ShowGeneralHelp();
            }
            else if (topic. Contains("event") || topic.Contains("generator"))
            {
                ShowEventGeneratorHelp();
            }
            else if (topic.Contains("metrics") || topic.Contains("logger"))
            {
                ShowMetricsLoggerHelp();
            }
            else
            {
                ShowGeneralHelp();
            }
        }

        private void ShowGeneralHelp()
        {
            Print("");
            Print("Microkernel Commands:");
            Print("=====================");
            Print("");
            Print("General:");
            Print("  help                  Show this help");
            Print("  help eventgenerator   Show EventGenerator plugin help");
            Print("  help metricslogger    Show MetricsLogger plugin help");
            Print("  status                Show kernel status");
            Print("  plugins               List plugin processes");
            Print("");
            Print("Events:");
            Print("  demo                  Run demo (sends test events)");
            Print("  userlogin [name]      Send UserLoggedInEvent");
            Print("  dataprocessed [count] Send DataProcessedEvent");
            Print("  metrics               Send SystemMetricsEvent");
            Print("  send <topic> [data]   Send custom event");
            Print("");
            Print("Event Generation:");
            Print("  generate              Show generate options");
            Print("  generate toggle       Toggle generation on/off");
            Print("  generate start/stop   Start or stop generation");
            Print("  generate <ms>         Set interval (e.g., generate 1000)");
            Print("");
            Print("Plugin Management:");
            Print("  load <name>           Load a plugin");
            Print("  unload <plugin>       Unload a plugin");
            Print("  crash <plugin>        Kill a plugin (fault isolation test)");
            Print("  restart <plugin>      Restart a plugin");
            Print("");
            Print("Output Control:");
            Print("  debug on|off          Toggle debug output");
            Print("  mute / unmute         Mute/unmute output");
            Print("  exit                  Shutdown and exit");
            Print("");
        }

        private void ShowEventGeneratorHelp()
        {
            Print("");
            Print("EventGenerator Plugin");
            Print("=====================");
            Print("");
            Print("Description:");
            Print("  Generates system metrics and events at regular intervals.");
            Print("  Collects real CPU, RAM, and Disk usage from your system.");
            Print("");
            Print("Commands:");
            Print("  generate              Show generate options");
            Print("  generate toggle       Toggle generation on/off");
            Print("  generate start        Start event generation");
            Print("  generate stop         Stop event generation");
            Print("  generate <ms>         Set interval in milliseconds");
            Print("");
            Print("Events Generated:");
            Print("  - SystemMetricsEvent  Real system metrics (CPU, RAM, Disk)");
            Print("  - UserLoggedInEvent   Random user login simulation (every 3rd cycle)");
            Print("  - DataProcessedEvent  Random data processing (every 5th cycle)");
            Print("  - alert. warning       When metrics exceed warning thresholds");
            Print("  - alert.critical      When metrics exceed critical thresholds");
            Print("");
            Print("Thresholds:");
            Print("  CPU Warning: 75%  | Critical: 90%");
            Print("  RAM Warning: 80%  | Critical: 90%");
            Print("  Disk Warning: 85% | Critical: 95%");
            Print("");
            Print("Default interval: 3000ms (3 seconds)");
            Print("Status: Generation is " + (_generating ? "ON" : "OFF"));
            Print("");
        }

        private void ShowMetricsLoggerHelp()
        {
            Print("");
            Print("MetricsLogger Plugin");
            Print("====================");
            Print("");
            Print("Description:");
            Print("  Receives events from other plugins and logs them to a file.");
            Print("  All events are timestamped and formatted for easy reading.");
            Print("");
            Print("Log File Location:");
            Print("  Logs/metrics_YYYY-MM-DD. log");
            Print("");
            Print("Events Logged:");
            Print("  - UserLoggedInEvent   -> USER_LOGIN | User: X | IP: X. X.X.X");
            Print("  - DataProcessedEvent  -> DATA_PROCESSED | Source: X | Records: N");
            Print("  - SystemMetricsEvent  -> SYSTEM_METRICS | CPU: X% | RAM: X% | Disk: X%");
            Print("  - Custom events       -> EVENT | Topic: X | Payload: .. .");
            Print("");
            Print("This plugin has no commands - it automatically logs all received events.");
            Print("");
        }

        private void ShowStatus()
        {
            var (total, running, faulted) = _kernel.GetPluginCounts();

            Print("");
            Print("Kernel Status: " + _kernel.State);
            Print("Plugins: " + total + " total, " + running + " running, " + faulted + " faulted");
            Print("Event Generation: " + (_generating ? "ON" : "OFF"));
            Print("");
        }

        private void ListPlugins()
        {
            var plugins = _kernel. GetLoadedPlugins();

            if (plugins.Count == 0)
            {
                Print("No plugins loaded.");
                return;
            }

            Print("");
            Print(string.Format("{0,-28} {1,-10} {2,-8} {3}", "Plugin", "State", "PID", "Started"));
            Print(new string('-', 60));

            foreach (var plugin in plugins)
            {
                if (! _muted)
                {
                    var color = plugin.State == "Running" ? ConsoleColor.Green :
                               plugin.State == "Faulted" ? ConsoleColor. Red : ConsoleColor.Yellow;

                    Console.ForegroundColor = color;
                    Console.WriteLine(string.Format("{0,-28} {1,-10} {2,-8} {3:HH:mm:ss}",
                        plugin.Name, plugin. State, plugin.ProcessId, plugin.LoadedAt));
                    Console.ResetColor();
                }
            }
            Print("");
        }

        private void RunDemo()
        {
            Print("");
            Print("=== Running Demo ===");
            Print("");

            Print("1.  Sending UserLoggedInEvent.. .");
            SendUserLoggedInEvent("");
            Thread.Sleep(300);

            Print("");
            Print("2. Sending DataProcessedEvent...");
            SendDataProcessedEvent("");
            Thread.Sleep(300);

            Print("");
            Print("3.  Sending SystemMetricsEvent...");
            SendSystemMetricsEvent();
            Thread.Sleep(300);

            Print("");
            Print("=== Demo Complete ===");
            Print("");
        }

        private void HandleGenerate(string args)
        {
            string trimmedArgs = (args ??  "").Trim(). ToLowerInvariant();

            if (string.IsNullOrEmpty(trimmedArgs))
            {
                ShowGenerateHelp();
                return;
            }

            if (trimmedArgs == "toggle")
            {
                if (_generating)
                {
                    StopGeneration();
                }
                else
                {
                    StartGeneration();
                }
                return;
            }

            if (trimmedArgs == "stop" || trimmedArgs == "off")
            {
                if (_generating)
                {
                    StopGeneration();
                }
                else
                {
                    Print("Event generation is not running.");
                }
                return;
            }

            if (trimmedArgs == "start" || trimmedArgs == "on")
            {
                if (!_generating)
                {
                    StartGeneration();
                }
                else
                {
                    Print("Event generation is already running.");
                }
                return;
            }

            if (int.TryParse(trimmedArgs, out int interval) && interval >= 100)
            {
                SetInterval(interval);

                if (!_generating)
                {
                    StartGeneration();
                }
                return;
            }

            Print("Invalid argument: " + args);
            ShowGenerateHelp();
        }

        private void ShowGenerateHelp()
        {
            Print("");
            Print("Generate Commands:");
            Print("==================");
            Print("  generate              Show this help");
            Print("  generate toggle       Toggle generation on/off");
            Print("  generate start        Start event generation");
            Print("  generate stop         Stop event generation");
            Print("  generate <ms>         Set interval in milliseconds");
            Print("");
            Print("Examples:");
            Print("  generate 1000         Generate events every 1 second");
            Print("  generate 500          Generate events every 0. 5 seconds");
            Print("  generate 5000         Generate events every 5 seconds");
            Print("");
            Print("Status: Generation is " + (_generating ? "ON" : "OFF"));
            Print("");
        }

        private void StartGeneration()
        {
            _generating = true;

            var startEvt = new EventMessage
            {
                Topic = "generator.start",
                Payload = "",
                Timestamp = DateTime. UtcNow,
                Source = "Console"
            };
            _kernel. Publish(startEvt);
            PrintColor("Event generation started.", ConsoleColor. Green);
        }

        private void StopGeneration()
        {
            _generating = false;

            var stopEvt = new EventMessage
            {
                Topic = "generator.stop",
                Payload = "",
                Timestamp = DateTime. UtcNow,
                Source = "Console"
            };
            _kernel. Publish(stopEvt);
            PrintColor("Event generation stopped.", ConsoleColor. Yellow);
        }

        private void SetInterval(int interval)
        {
            var intervalEvt = new EventMessage
            {
                Topic = "generator.interval",
                Payload = interval.ToString(),
                Timestamp = DateTime.UtcNow,
                Source = "Console"
            };
            _kernel. Publish(intervalEvt);
            Print("Interval set to " + interval + "ms");
        }

        private void SendUserLoggedInEvent(string username)
        {
            string[] names = { "alice", "bob", "charlie", "diana", "eve" };
            string[] ips = { "192.168.1.100", "10.0.0.50", "172.16.0.25" };

            var userEvent = new UserLoggedInEvent
            {
                UserId = Guid.NewGuid().ToString(),
                Username = string.IsNullOrWhiteSpace(username) ? names[_random.Next(names.Length)] : username,
                IpAddress = ips[_random. Next(ips. Length)]
            };

            var evt = new EventMessage
            {
                Topic = "UserLoggedInEvent",
                Payload = JsonSerializer.Serialize(userEvent, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                Timestamp = DateTime.UtcNow,
                Source = "Console"
            };

            _kernel.Publish(evt);
            Print("  -> User: " + userEvent.Username + ", IP: " + userEvent.IpAddress);
        }

        private void SendDataProcessedEvent(string args)
        {
            string[] sources = { "CustomerDB", "OrdersDB", "InventoryDB" };

            int recordCount = 0;
            if (! string.IsNullOrWhiteSpace(args) && int.TryParse(args. Trim(), out int parsed))
            {
                recordCount = parsed;
            }

            var dataEvent = new DataProcessedEvent
            {
                DataSource = sources[_random.Next(sources. Length)],
                RecordsProcessed = recordCount > 0 ? recordCount : _random.Next(100, 5000),
                ProcessingTimeMs = _random. NextDouble() * 1000,
                Success = _random.Next(10) != 0
            };

            if (! dataEvent.Success)
                dataEvent.ErrorMessage = "Simulated error";

            var evt = new EventMessage
            {
                Topic = "DataProcessedEvent",
                Payload = JsonSerializer.Serialize(dataEvent, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                Timestamp = DateTime. UtcNow,
                Source = "Console"
            };

            _kernel. Publish(evt);
            Print("  -> Source: " + dataEvent.DataSource + ", Records: " + dataEvent.RecordsProcessed + ", Success: " + dataEvent.Success);
        }

        private void SendSystemMetricsEvent()
        {
            double cpu = 20 + _random. NextDouble() * 60;
            double ram = 40 + _random. NextDouble() * 40;
            double disk = 50 + _random. NextDouble() * 30;

            try
            {
                var drive = new DriveInfo("C");
                disk = ((drive.TotalSize - drive. AvailableFreeSpace) / (double)drive.TotalSize) * 100;
            }
            catch { }

            var metricsEvent = new SystemMetricsEvent
            {
                MachineName = Environment.MachineName,
                CpuUsagePercent = cpu,
                MemoryUsagePercent = ram,
                DiskUsagePercent = disk,
                Timestamp = DateTime. UtcNow
            };

            var evt = new EventMessage
            {
                Topic = "SystemMetricsEvent",
                Payload = JsonSerializer.Serialize(metricsEvent, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                Timestamp = DateTime.UtcNow,
                Source = "Console"
            };

            _kernel.Publish(evt);
            Print("  -> CPU: " + cpu. ToString("F1") + "%, RAM: " + ram. ToString("F1") + "%, Disk: " + disk. ToString("F1") + "%");
        }

        private void SendCustomEvent(string args)
        {
            var parts = args.Split(' ', 2);
            string topic = parts[0];
            string payload = parts.Length > 1 ? parts[1] : "";

            if (string.IsNullOrWhiteSpace(topic))
            {
                Print("Usage: send <topic> [payload]");
                return;
            }

            var evt = new EventMessage
            {
                Topic = topic,
                Payload = payload,
                Timestamp = DateTime. UtcNow,
                Source = "Console"
            };

            _kernel.Publish(evt);
            Print("Event sent: " + topic);
        }

        private void LoadPlugin(string args)
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                Print("Usage: load <plugin-name>");
                Print("Available: MetricsLogger, EventGenerator");
                return;
            }

            string input = args.Trim();
            string exePath;

            if (Path.IsPathRooted(input))
            {
                exePath = input;
            }
            else if (input.EndsWith(".exe", StringComparison. OrdinalIgnoreCase))
            {
                exePath = Path. Combine(AppDomain.CurrentDomain.BaseDirectory, input);
            }
            else
            {
                exePath = Path. Combine(AppDomain.CurrentDomain. BaseDirectory, input + ".exe");
            }

            if (!File.Exists(exePath))
            {
                Print("File not found: " + exePath);
                Print("");
                Print("Available plugins:");
                try
                {
                    var exeFiles = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "*.exe")
                        .Where(f => !Path.GetFileName(f). Equals("Microkernel.exe", StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    foreach (var file in exeFiles)
                    {
                        Print("  " + Path.GetFileNameWithoutExtension(file));
                    }
                }
                catch { }
                return;
            }

            string pluginName = Path.GetFileNameWithoutExtension(exePath) + "Process";

            Print("Loading plugin: " + pluginName);

            if (_kernel.LoadPlugin(pluginName, exePath))
            {
                Thread.Sleep(500);
                PrintColor("Plugin loaded successfully.", ConsoleColor. Green);
            }
            else
            {
                PrintColor("Failed to load plugin.", ConsoleColor.Red);
            }
        }

        private void UnloadPlugin(string pluginName)
        {
            if (string.IsNullOrWhiteSpace(pluginName))
            {
                Print("Usage: unload <plugin>");
                return;
            }

            var plugins = _kernel.GetLoadedPlugins();
            var match = plugins.FirstOrDefault(p =>
                p.Name.Contains(pluginName, StringComparison. OrdinalIgnoreCase));

            if (match == null)
            {
                Print("Plugin not found: " + pluginName);
                return;
            }

            Print("Unloading plugin: " + match. Name);

            if (_kernel.UnloadPlugin(match.Name))
            {
                PrintColor("Plugin unloaded.", ConsoleColor.Green);
            }
            else
            {
                Print("Failed to unload plugin.");
            }
        }

        private void CrashPlugin(string pluginName)
        {
            if (string.IsNullOrWhiteSpace(pluginName))
            {
                Print("Usage: crash <plugin>");
                return;
            }

            var plugins = _kernel.GetLoadedPlugins();
            var match = plugins. FirstOrDefault(p =>
                p. Name.Contains(pluginName, StringComparison.OrdinalIgnoreCase));

            if (match == null)
            {
                Print("Plugin not found: " + pluginName);
                return;
            }

            Print("Crashing plugin: " + match. Name);

            if (_kernel.CrashPlugin(match. Name))
            {
                PrintColor("Plugin crashed.  Kernel and other plugins continue running.", ConsoleColor. Yellow);
            }
            else
            {
                Print("Failed to crash plugin.");
            }
        }

        private void RestartPlugin(string pluginName)
        {
            if (string.IsNullOrWhiteSpace(pluginName))
            {
                Print("Usage: restart <plugin>");
                return;
            }

            var plugins = _kernel.GetLoadedPlugins();
            var match = plugins.FirstOrDefault(p =>
                p.Name.Contains(pluginName, StringComparison. OrdinalIgnoreCase));

            if (match == null)
            {
                Print("Plugin not found: " + pluginName);
                return;
            }

            Print("Restarting plugin: " + match.Name);

            if (_kernel.RestartPlugin(match.Name))
            {
                PrintColor("Plugin restarted successfully.", ConsoleColor. Green);
            }
            else
            {
                Print("Failed to restart plugin.");
            }
        }

        private void ToggleDebug(string args)
        {
            if (args.Equals("on", StringComparison.OrdinalIgnoreCase))
            {
                ConsoleKernelLogger.EnableDebug();
                Print("Debug output enabled.");
            }
            else if (args.Equals("off", StringComparison.OrdinalIgnoreCase))
            {
                ConsoleKernelLogger.DisableDebug();
                Print("Debug output disabled.");
            }
            else
            {
                Print("Usage: debug on|off");
            }
        }
    }
}