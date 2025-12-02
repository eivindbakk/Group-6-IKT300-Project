using System;
using System.IO;
using System. IO.Pipes;
using System.Runtime.  InteropServices;
using System.Text.  Json;
using System.Threading;
using Contracts;
using Contracts.Events;
using Contracts.IPC;

namespace EventGeneratorPlugin
{
    class Program
    {
        private static string _pipeName;
        private static NamedPipeClientStream _pipeClient;
        private static bool _running = true;
        private static bool _generating = false;
        private static int _intervalMs = 3000;
        private static int _eventsGenerated = 0;
        private static Random _random = new Random();
        private static Thread _generatorThread;
        private static readonly object _lock = new object();

        static void Main(string[] args)
        {
            Console.WriteLine("[EventGenerator] Starting.. .");

            _pipeName = GetPipeNameFromArgs(args);
            if (string.IsNullOrEmpty(_pipeName))
            {
                Console.WriteLine("[EventGenerator] ERROR: No pipe name specified.");
                return;
            }

            try
            {
                ConnectAndListen();
            }
            catch (Exception ex)
            {
                Console.WriteLine("[EventGenerator] Fatal error: " + ex.Message);
            }

            Console.WriteLine("[EventGenerator] Shutting down.  Events generated: " + _eventsGenerated);
        }

        private static string GetPipeNameFromArgs(string[] args)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--pipe")
                    return args[i + 1];
            }
            return null;
        }

        private static void ConnectAndListen()
        {
            using (_pipeClient = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions. Asynchronous))
            {
                Console.WriteLine("[EventGenerator] Connecting.. .");
                _pipeClient.Connect(30000);
                Console.WriteLine("[EventGenerator] Connected!");

                SendMessage(new IpcMessage
                {
                    Type = IpcMessageType. Ack,
                    PluginName = "EventGenerator",
                    Response = "Ready"
                });

                var heartbeatThread = new Thread(SendHeartbeats);
                heartbeatThread.IsBackground = true;
                heartbeatThread.Start();

                while (_running && _pipeClient.IsConnected)
                {
                    try
                    {
                        var message = IpcProtocol.ReadMessage(_pipeClient);
                        if (message == null)
                        {
                            Thread.Sleep(10);
                            continue;
                        }

                        HandleMessage(message);
                    }
                    catch (IOException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[EventGenerator] Error: " + ex.Message);
                    }
                }
            }
        }

        private static void SendHeartbeats()
        {
            while (_running && _pipeClient != null && _pipeClient. IsConnected)
            {
                try
                {
                    Thread.Sleep(5000);
                    if (_pipeClient. IsConnected)
                    {
                        SendMessage(new IpcMessage
                        {
                            Type = IpcMessageType.Heartbeat,
                            PluginName = "EventGenerator"
                        });
                    }
                }
                catch { break; }
            }
        }

        private static void HandleMessage(IpcMessage message)
        {
            switch (message.Type)
            {
                case IpcMessageType.Start:
                    Console.WriteLine("[EventGenerator] Received START from kernel.");
                    break;

                case IpcMessageType. Shutdown:
                    Console.WriteLine("[EventGenerator] Received SHUTDOWN.");
                    StopGenerating();
                    _running = false;
                    break;

                case IpcMessageType. Event:
                    if (message.Event != null)
                        HandleEvent(message.Event);
                    break;
            }
        }

        private static void HandleEvent(EventMessage evt)
        {
            if (evt == null) return;

            string topic = (evt.Topic ??  "").ToLowerInvariant(). Trim();
            string payload = (evt.Payload ??  ""). Trim();

            Console.WriteLine("[EventGenerator] Received: " + topic);

            switch (topic)
            {
                case "eventgenerator.start":
                case "generator.start":
                    StartGenerating();
                    break;

                case "eventgenerator.stop":
                case "generator.stop":
                    StopGenerating();
                    break;

                case "eventgenerator.now":
                case "generator.now":
                    GenerateSystemMetricsEvent();
                    break;

                case "eventgenerator.interval":
                case "generator.interval":
                    if (int.TryParse(payload, out int intervalSec) && intervalSec > 0)
                    {
                        _intervalMs = intervalSec * 1000;
                        Console.WriteLine("[EventGenerator] Interval: " + intervalSec + "s");
                    }
                    else if (int.TryParse(payload, out int intervalMs) && intervalMs >= 100)
                    {
                        _intervalMs = intervalMs;
                        Console. WriteLine("[EventGenerator] Interval: " + intervalMs + "ms");
                    }
                    break;
            }
        }

        private static void StartGenerating()
        {
            lock (_lock)
            {
                if (_generating)
                {
                    Console. WriteLine("[EventGenerator] Already generating.");
                    return;
                }

                _generating = true;
                _generatorThread = new Thread(GeneratorLoop);
                _generatorThread.IsBackground = true;
                _generatorThread.Start();

                Console.WriteLine("[EventGenerator] Started.  Interval: " + _intervalMs + "ms");
            }
        }

        private static void StopGenerating()
        {
            lock (_lock)
            {
                if (!_generating) return;
                _generating = false;
                Console.WriteLine("[EventGenerator] Stopped.");
            }
        }

        private static void GeneratorLoop()
        {
            while (_generating && _running && _pipeClient != null && _pipeClient.IsConnected)
            {
                try
                {
                    // Generate system metrics every cycle
                    GenerateSystemMetricsEvent();
                    _eventsGenerated++;

                    // Also generate other events periodically
                    if (_eventsGenerated % 3 == 0)
                    {
                        GenerateUserLoggedInEvent();
                        _eventsGenerated++;
                    }

                    if (_eventsGenerated % 5 == 0)
                    {
                        GenerateDataProcessedEvent();
                        _eventsGenerated++;
                    }

                    Thread. Sleep(_intervalMs);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[EventGenerator] Error: " + ex.Message);
                    Thread.Sleep(1000);
                }
            }
        }

        #region Event Generators

        private static void GenerateUserLoggedInEvent()
        {
            string[] usernames = { "alice", "bob", "charlie", "diana", "eve", "frank", "grace" };
            string[] ips = { "192.168.1. 100", "10.0.0. 50", "172.16.0.25", "192.168.0.1" };

            var userEvent = new UserLoggedInEvent
            {
                UserId = Guid.NewGuid().ToString(),
                Username = usernames[_random.Next(usernames.Length)],
                IpAddress = ips[_random.Next(ips.Length)]
            };

            var eventMessage = new EventMessage
            {
                Topic = "UserLoggedInEvent",
                Payload = JsonSerializer. Serialize(userEvent, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                Timestamp = DateTime.UtcNow,
                Source = "EventGenerator"
            };

            PublishEvent(eventMessage);
            Console.WriteLine("[EventGenerator] UserLoggedIn: " + userEvent.Username);
        }

        private static void GenerateDataProcessedEvent()
        {
            string[] sources = { "CustomerDB", "OrdersDB", "InventoryDB", "AnalyticsDB" };

            var dataEvent = new DataProcessedEvent
            {
                DataSource = sources[_random.Next(sources.Length)],
                RecordsProcessed = _random.Next(100, 10000),
                ProcessingTimeMs = _random. NextDouble() * 500,
                Success = _random.Next(10) != 0
            };

            if (! dataEvent.Success)
                dataEvent.ErrorMessage = "Simulated processing error";

            var eventMessage = new EventMessage
            {
                Topic = "DataProcessedEvent",
                Payload = JsonSerializer.Serialize(dataEvent, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                Timestamp = DateTime. UtcNow,
                Source = "EventGenerator"
            };

            PublishEvent(eventMessage);
            Console.WriteLine("[EventGenerator] DataProcessed: " + dataEvent.DataSource);
        }

        private static void GenerateSystemMetricsEvent()
        {
            double cpuUsage = GetCpuUsage();
            double memoryUsage = GetSystemMemoryUsage();
            double diskUsage = GetDiskUsage();

            var metricsEvent = new SystemMetricsEvent
            {
                MachineName = Environment. MachineName,
                CpuUsagePercent = cpuUsage,
                MemoryUsagePercent = memoryUsage,
                DiskUsagePercent = diskUsage,
                Timestamp = DateTime. UtcNow
            };

            var eventMessage = new EventMessage
            {
                Topic = "SystemMetricsEvent",
                Payload = JsonSerializer.Serialize(metricsEvent, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy. CamelCase }),
                Timestamp = DateTime.UtcNow,
                Source = "EventGenerator"
            };

            PublishEvent(eventMessage);
            Console.WriteLine("[EventGenerator] SystemMetrics: CPU=" + cpuUsage. ToString("F1") + 
                              "% RAM=" + memoryUsage.ToString("F1") + 
                              "% Disk=" + diskUsage.ToString("F1") + "%");

            // Check thresholds and publish alerts
            CheckThresholds(cpuUsage, memoryUsage, diskUsage);
        }

        #endregion

        #region System Metrics Collection

        private static double GetCpuUsage()
        {
            // Base usage between 5-25%
            double baseUsage = 5 + (_random.NextDouble() * 20);

            // 10% chance of medium usage (25-50%)
            if (_random.Next(10) == 0)
            {
                baseUsage = 25 + (_random.NextDouble() * 25);
            }

            // 5% chance of high spike (50-80%)
            if (_random.Next(20) == 0)
            {
                baseUsage = 50 + (_random.NextDouble() * 30);
            }

            return Math.Min(100, baseUsage);
        }

        // Windows API for real memory usage
        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType. Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        private static double GetSystemMemoryUsage()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform. Windows))
                {
                    MEMORYSTATUSEX memStatus = new MEMORYSTATUSEX();
                    memStatus.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));

                    if (GlobalMemoryStatusEx(ref memStatus))
                    {
                        return memStatus. dwMemoryLoad;
                    }
                }
            }
            catch
            {
                // Fall through to simulated value
            }

            // Fallback: simulated memory usage
            return 40 + (_random.NextDouble() * 40);
        }

        private static double GetDiskUsage()
        {
            try
            {
                var drive = new DriveInfo("C");
                double used = drive.TotalSize - drive.AvailableFreeSpace;
                return (used / drive.TotalSize) * 100;
            }
            catch
            {
                return 50 + (_random.NextDouble() * 30);
            }
        }

        private static void CheckThresholds(double cpu, double memory, double disk)
        {
            // CPU alerts
            if (cpu > 90)
            {
                PublishAlert("alert.critical", "CPU critical: " + cpu. ToString("F1") + "%");
            }
            else if (cpu > 75)
            {
                PublishAlert("alert.warning", "CPU high: " + cpu. ToString("F1") + "%");
            }

            // Memory alerts
            if (memory > 90)
            {
                PublishAlert("alert.critical", "Memory critical: " + memory.ToString("F1") + "%");
            }
            else if (memory > 80)
            {
                PublishAlert("alert.warning", "Memory high: " + memory.ToString("F1") + "%");
            }

            // Disk alerts
            if (disk > 95)
            {
                PublishAlert("alert.critical", "Disk almost full: " + disk.ToString("F1") + "%");
            }
            else if (disk > 85)
            {
                PublishAlert("alert.warning", "Disk space low: " + disk. ToString("F1") + "%");
            }
        }

        private static void PublishAlert(string topic, string message)
        {
            var alertEvent = new EventMessage
            {
                Topic = topic,
                Payload = message,
                Timestamp = DateTime.UtcNow,
                Source = "EventGenerator"
            };

            PublishEvent(alertEvent);
            Console.WriteLine("[EventGenerator] ALERT: " + message);
        }

        #endregion

        #region IPC

        private static void PublishEvent(EventMessage evt)
        {
            try
            {
                SendMessage(new IpcMessage
                {
                    Type = IpcMessageType. Publish,
                    PluginName = "EventGenerator",
                    Event = evt
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine("[EventGenerator] Publish error: " + ex.Message);
            }
        }

        private static void SendMessage(IpcMessage message)
        {
            try
            {
                if (_pipeClient != null && _pipeClient. IsConnected)
                {
                    IpcProtocol. WriteMessage(_pipeClient, message);
                }
            }
            catch { }
        }

        #endregion
    }
}