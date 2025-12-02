using System;
using System. IO;
using System. IO. Pipes;
using System.Text. Json;
using System.Threading;
using Contracts;
using Contracts.Events;
using Contracts.IPC;

namespace EventGeneratorPlugin
{
    /// <summary>
    /// EventGenerator plugin running as a SEPARATE OS PROCESS. 
    /// Communicates with the Microkernel via Named Pipes (IPC).
    /// Generates UserLoggedInEvent and DataProcessedEvent periodically.
    /// </summary>
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

        static void Main(string[] args)
        {
            Console.WriteLine("[EventGenerator Process] Starting...");
            Console.WriteLine("[EventGenerator Process] PID: " + Environment.ProcessId);

            _pipeName = GetPipeNameFromArgs(args);
            if (string.IsNullOrEmpty(_pipeName))
            {
                Console.WriteLine("[EventGenerator Process] ERROR: No pipe name specified.  Use --pipe <name>");
                Console.WriteLine("[EventGenerator Process] Running in standalone mode for testing...");
                RunStandaloneMode();
                return;
            }

            Console.WriteLine("[EventGenerator Process] Connecting to pipe: " + _pipeName);

            try
            {
                ConnectAndListen();
            }
            catch (Exception ex)
            {
                Console.WriteLine("[EventGenerator Process] Fatal error: " + ex.Message);
            }

            Console.WriteLine("[EventGenerator Process] Shutting down...");
            Console.WriteLine("[EventGenerator Process] Total events generated: " + _eventsGenerated);
        }

        private static string GetPipeNameFromArgs(string[] args)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--pipe" || args[i] == "-p")
                {
                    return args[i + 1];
                }
            }
            return null;
        }

        private static void ConnectAndListen()
        {
            using (_pipeClient = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions. Asynchronous))
            {
                Console.WriteLine("[EventGenerator Process] Connecting to kernel...");
                _pipeClient.Connect(30000);
                Console.WriteLine("[EventGenerator Process] Connected to kernel!");

                // Send ready message
                SendMessage(new IpcMessage
                {
                    Type = IpcMessageType. Ack,
                    PluginName = "EventGenerator",
                    Response = "Ready"
                });

                // Start heartbeat thread
                var heartbeatThread = new Thread(SendHeartbeats);
                heartbeatThread.IsBackground = true;
                heartbeatThread.Start();

                // Listen for messages from kernel
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
                        Console.WriteLine("[EventGenerator Process] Pipe disconnected.");
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[EventGenerator Process] Error: " + ex.Message);
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
                            Type = IpcMessageType. Heartbeat,
                            PluginName = "EventGenerator"
                        });
                    }
                }
                catch
                {
                    break;
                }
            }
        }

        private static void HandleMessage(IpcMessage message)
        {
            switch (message.Type)
            {
                case IpcMessageType. Event:
                    HandleEvent(message.Event);
                    break;

                case IpcMessageType.Start:
                    Console.WriteLine("[EventGenerator Process] Received START command.");
                    StartGenerating();
                    break;

                case IpcMessageType. Stop:
                    Console.WriteLine("[EventGenerator Process] Received STOP command.");
                    StopGenerating();
                    break;

                case IpcMessageType. Shutdown:
                    Console.WriteLine("[EventGenerator Process] Received SHUTDOWN command.");
                    StopGenerating();
                    _running = false;
                    break;

                default:
                    Console.WriteLine("[EventGenerator Process] Unknown message type: " + message.Type);
                    break;
            }
        }

        private static void HandleEvent(EventMessage evt)
        {
            if (evt == null) return;

            string topic = evt.Topic ??  "";
            string payload = evt. Payload ?? "";

            // Handle control commands sent as events
            if (topic. Equals("generator. start", StringComparison.OrdinalIgnoreCase))
            {
                StartGenerating();
            }
            else if (topic.Equals("generator.stop", StringComparison.OrdinalIgnoreCase))
            {
                StopGenerating();
            }
            else if (topic.Equals("generator.interval", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(payload, out int interval) && interval >= 100)
                {
                    _intervalMs = interval;
                    Console. WriteLine("[EventGenerator Process] Interval set to: " + _intervalMs + "ms");
                }
            }
            else if (topic. Equals("generator.status", StringComparison.OrdinalIgnoreCase))
            {
                string status = _generating ? "Running" : "Stopped";
                Console.WriteLine("[EventGenerator Process] Status: " + status + ", Events: " + _eventsGenerated + ", Interval: " + _intervalMs + "ms");
            }
        }

        private static void StartGenerating()
        {
            if (_generating)
            {
                Console. WriteLine("[EventGenerator Process] Already generating events.");
                return;
            }

            _generating = true;
            _generatorThread = new Thread(GeneratorLoop);
            _generatorThread.IsBackground = true;
            _generatorThread.Start();

            Console.WriteLine("[EventGenerator Process] Started generating events (interval: " + _intervalMs + "ms)");
        }

        private static void StopGenerating()
        {
            if (!_generating)
            {
                Console.WriteLine("[EventGenerator Process] Already stopped.");
                return;
            }

            _generating = false;
            Console. WriteLine("[EventGenerator Process] Stopped generating events.  Total: " + _eventsGenerated);
        }

        private static void GeneratorLoop()
        {
            while (_generating && _running && _pipeClient != null && _pipeClient.IsConnected)
            {
                try
                {
                    // Alternate between UserLoggedInEvent and DataProcessedEvent
                    if (_eventsGenerated % 2 == 0)
                    {
                        GenerateUserLoggedInEvent();
                    }
                    else
                    {
                        GenerateDataProcessedEvent();
                    }

                    _eventsGenerated++;
                    Thread.Sleep(_intervalMs);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[EventGenerator Process] Generator error: " + ex.Message);
                    Thread.Sleep(1000);
                }
            }
        }

        private static void GenerateUserLoggedInEvent()
        {
            string[] usernames = { "alice", "bob", "charlie", "diana", "eve", "frank", "grace", "henry" };
            string[] ips = { "192.168.1. 100", "192.168.1.101", "10.0.0. 50", "10.0.0.51", "172.16.0. 25" };

            var userEvent = new UserLoggedInEvent
            {
                UserId = Guid.NewGuid().ToString(),
                Username = usernames[_random.Next(usernames.Length)],
                IpAddress = ips[_random.Next(ips.Length)],
                LoginTime = DateTime. UtcNow,
                SessionId = Guid.NewGuid().ToString()
            };

            var eventMessage = new EventMessage
            {
                Topic = "UserLoggedInEvent",
                Payload = JsonSerializer. Serialize(userEvent, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                Timestamp = DateTime. UtcNow,
                Source = "EventGenerator"
            };

            PublishEvent(eventMessage);
            Console. WriteLine("[EventGenerator Process] Generated UserLoggedInEvent for: " + userEvent.Username);
        }

        private static void GenerateDataProcessedEvent()
        {
            string[] sources = { "CustomerDB", "OrdersDB", "InventoryDB", "AnalyticsDB", "LogsDB", "MetricsDB" };

            var dataEvent = new DataProcessedEvent
            {
                ProcessId = Guid.NewGuid().ToString(),
                DataSource = sources[_random.Next(sources. Length)],
                RecordsProcessed = _random.Next(100, 10000),
                ProcessedAt = DateTime. UtcNow,
                ProcessingTimeMs = _random. NextDouble() * 2000,
                Success = _random.Next(10) != 0  // 90% success rate
            };

            if (! dataEvent.Success)
            {
                string[] errors = { "Connection timeout", "Invalid data format", "Disk full", "Permission denied" };
                dataEvent.ErrorMessage = errors[_random. Next(errors.Length)];
            }

            var eventMessage = new EventMessage
            {
                Topic = "DataProcessedEvent",
                Payload = JsonSerializer. Serialize(dataEvent, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy. CamelCase }),
                Timestamp = DateTime.UtcNow,
                Source = "EventGenerator"
            };

            PublishEvent(eventMessage);

            string status = dataEvent.Success ? "SUCCESS" : "FAILED";
            Console. WriteLine("[EventGenerator Process] Generated DataProcessedEvent: " + status + " - " + dataEvent. RecordsProcessed + " records from " + dataEvent. DataSource);
        }

        private static void PublishEvent(EventMessage evt)
        {
            try
            {
                var message = new IpcMessage
                {
                    Type = IpcMessageType. Publish,
                    PluginName = "EventGenerator",
                    Event = evt
                };
                SendMessage(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[EventGenerator Process] Failed to publish event: " + ex.Message);
            }
        }

        private static void SendMessage(IpcMessage message)
        {
            try
            {
                if (_pipeClient != null && _pipeClient.IsConnected)
                {
                    IpcProtocol. WriteMessage(_pipeClient, message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[EventGenerator Process] SendMessage error: " + ex.Message);
            }
        }

        private static void RunStandaloneMode()
        {
            Console.WriteLine("[EventGenerator Process] Running in standalone test mode.");
            Console.WriteLine("[EventGenerator Process] Will generate events to console only.");
            Console. WriteLine("[EventGenerator Process] Press 'S' to start, 'X' to stop, 'Q' to quit.");

            while (true)
            {
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.S)
                {
                    Console.WriteLine("Starting generator...");
                    _generating = true;
                    new Thread(() =>
                    {
                        while (_generating)
                        {
                            Console.WriteLine("[STANDALONE] Would generate event #" + (++_eventsGenerated));
                            Thread. Sleep(_intervalMs);
                        }
                    }).Start();
                }
                else if (key.Key == ConsoleKey.X)
                {
                    Console.WriteLine("Stopping generator...");
                    _generating = false;
                }
                else if (key.Key == ConsoleKey.Q)
                {
                    _generating = false;
                    break;
                }
            }
        }
    }
}