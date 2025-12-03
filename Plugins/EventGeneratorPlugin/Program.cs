using System;
using System. IO;
using System. IO.Pipes;
using System. Runtime.InteropServices;
using System.Text. Json;
using System. Threading;
using Contracts;
using Contracts. Events;
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

        // CPU tracking using Windows API
        private static long _lastTotalTime = 0;
        private static long _lastIdleTime = 0;
        private static bool _cpuInitialized = false;

        static void Main(string[] args)
        {
            Console.WriteLine("[EventGenerator] Starting...");

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
            for (int i = 0; i < args. Length - 1; i++)
            {
                if (args[i] == "--pipe")
                    return args[i + 1];
            }
            return null;
        }

        private static void ConnectAndListen()
        {
            using (_pipeClient = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous))
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

                while (_running && _pipeClient. IsConnected)
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

            string topic = (evt.Topic ?? "").ToLowerInvariant(). Trim();
            string payload = (evt. Payload ?? ""). Trim();

            Console.WriteLine("[EventGenerator] Received: " + topic);

            // Use constants (compare lowercase since we lowered the topic)
            if (topic == EventTopics.GeneratorStart.ToLowerInvariant() ||
                topic == "eventgenerator.start")
            {
                StartGenerating();
            }
            else if (topic == EventTopics.GeneratorStop.ToLowerInvariant() ||
                     topic == "eventgenerator.stop")
            {
                StopGenerating();
            }
            else if (topic == EventTopics.GeneratorNow.ToLowerInvariant() ||
                     topic == "eventgenerator. now")
            {
                GenerateSystemMetricsEvent();
            }
            else if (topic == EventTopics.GeneratorInterval.ToLowerInvariant() ||
                     topic == "eventgenerator.interval")
            {
                if (int.TryParse(payload, out int interval) && interval >= 100)
                {
                    _intervalMs = interval;
                    Console.WriteLine("[EventGenerator] Interval: " + interval + "ms");
                }
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
                    GenerateSystemMetricsEvent();
                    _eventsGenerated++;

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

                    Thread.Sleep(_intervalMs);
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
            string[] ips = { "192.168.1. 100", "10.0.0. 50", "172.16.0. 25", "192.168.0.1" };

            var userEvent = new UserLoggedInEvent
            {
                UserId = Guid.NewGuid().ToString(),
                Username = usernames[_random. Next(usernames. Length)],
                IpAddress = ips[_random.Next(ips.Length)]
            };

            var eventMessage = new EventMessage
            {
                Topic = EventTopics.UserLoggedIn,
                Payload = JsonSerializer. Serialize(userEvent, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                Timestamp = DateTime. UtcNow,
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
                DataSource = sources[_random.Next(sources. Length)],
                RecordsProcessed = _random.Next(100, 10000),
                ProcessingTimeMs = _random.NextDouble() * 500,
                Success = _random.Next(10) != 0
            };

            if (! dataEvent.Success)
                dataEvent.ErrorMessage = "Simulated processing error";

            var eventMessage = new EventMessage
            {
                Topic = EventTopics. DataProcessed,
                Payload = JsonSerializer.Serialize(dataEvent, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                Timestamp = DateTime. UtcNow,
                Source = "EventGenerator"
            };

            PublishEvent(eventMessage);
            Console. WriteLine("[EventGenerator] DataProcessed: " + dataEvent.DataSource);
        }

        private static void GenerateSystemMetricsEvent()
        {
            double cpuUsage = GetCpuUsage();
            double memoryUsage = GetSystemMemoryUsage();
            double diskActivity = GetDiskActivity();

            var metricsEvent = new SystemMetricsEvent
            {
                MachineName = Environment.MachineName,
                CpuUsagePercent = cpuUsage,
                MemoryUsagePercent = memoryUsage,
                DiskUsagePercent = diskActivity,
                Timestamp = DateTime. UtcNow
            };

            var eventMessage = new EventMessage
            {
                Topic = EventTopics.SystemMetrics,
                Payload = JsonSerializer.Serialize(metricsEvent, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy. CamelCase }),
                Timestamp = DateTime.UtcNow,
                Source = "EventGenerator"
            };

            PublishEvent(eventMessage);
            Console.WriteLine("[EventGenerator] SystemMetrics: CPU=" + cpuUsage. ToString("F1") +
                              "% RAM=" + memoryUsage.ToString("F1") +
                              "% Disk=" + diskActivity.ToString("F1") + "%");

            CheckThresholds(cpuUsage, memoryUsage, diskActivity);
        }

        #endregion

        #region System Metrics Collection

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemTimes(out long idleTime, out long kernelTime, out long userTime);

        private static double GetCpuUsage()
        {
            try
            {
                if (GetSystemTimes(out long idleTime, out long kernelTime, out long userTime))
                {
                    long totalTime = kernelTime + userTime;

                    if (_cpuInitialized)
                    {
                        long totalDiff = totalTime - _lastTotalTime;
                        long idleDiff = idleTime - _lastIdleTime;

                        if (totalDiff > 0)
                        {
                            double cpuUsage = (1.0 - ((double)idleDiff / totalDiff)) * 100.0;

                            _lastTotalTime = totalTime;
                            _lastIdleTime = idleTime;

                            return Math.Max(0, Math.Min(100, cpuUsage));
                        }
                    }

                    _lastTotalTime = totalTime;
                    _lastIdleTime = idleTime;
                    _cpuInitialized = true;

                    return 0;
                }
            }
            catch { }

            return 10 + (_random.NextDouble() * 20);
        }

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
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        private static double GetSystemMemoryUsage()
        {
            try
            {
                MEMORYSTATUSEX memStatus = new MEMORYSTATUSEX();
                memStatus.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));

                if (GlobalMemoryStatusEx(ref memStatus))
                {
                    return memStatus.dwMemoryLoad;
                }
            }
            catch { }

            return 40 + (_random. NextDouble() * 40);
        }

        private static double GetDiskActivity()
        {
            // Simulated disk activity - real disk I/O monitoring requires admin or PerformanceCounter
            // which causes timeout issues
            double baseActivity = 2 + (_random.NextDouble() * 8);
            
            if (_random.Next(20) == 0)
                baseActivity = 20 + (_random. NextDouble() * 30);
            
            if (_random.Next(50) == 0)
                baseActivity = 50 + (_random.NextDouble() * 40);

            return Math.Min(100, baseActivity);
        }

        private static void CheckThresholds(double cpu, double memory, double disk)
        {
            if (cpu > 90)
            {
                PublishAlert(EventTopics.AlertCritical, "CPU critical: " + cpu.ToString("F1") + "%");
            }
            else if (cpu > 75)
            {
                PublishAlert(EventTopics.AlertWarning, "CPU high: " + cpu. ToString("F1") + "%");
            }

            if (memory > 90)
            {
                PublishAlert(EventTopics.AlertCritical, "Memory critical: " + memory.ToString("F1") + "%");
            }
            else if (memory > 80)
            {
                PublishAlert(EventTopics.AlertWarning, "Memory high: " + memory.ToString("F1") + "%");
            }

            if (disk > 90)
            {
                PublishAlert(EventTopics.AlertCritical, "Disk I/O critical: " + disk.ToString("F1") + "%");
            }
            else if (disk > 70)
            {
                PublishAlert(EventTopics.AlertWarning, "Disk I/O high: " + disk.ToString("F1") + "%");
            }
        }

        private static void PublishAlert(string topic, string message)
        {
            var alertEvent = new EventMessage
            {
                Topic = topic,
                Payload = message,
                Timestamp = DateTime. UtcNow,
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