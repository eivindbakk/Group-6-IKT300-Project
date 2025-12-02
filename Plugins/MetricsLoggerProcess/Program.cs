using System;
using System. IO;
using System. IO.Pipes;
using System.Text. Json;
using System.Threading;
using Contracts;
using Contracts. Events;
using Contracts.IPC;

namespace MetricsLoggerProcess
{
    /// <summary>
    /// MetricsLogger plugin running as a SEPARATE OS PROCESS. 
    /// Communicates with the Microkernel via Named Pipes (IPC).
    /// This satisfies the assignment requirement for process isolation.
    /// </summary>
    class Program
    {
        private static string _pipeName;
        private static string _logFilePath;
        private static int _eventsReceived = 0;
        private static int _userLoginsLogged = 0;
        private static int _dataProcessedLogged = 0;
        private static bool _running = true;

        static void Main(string[] args)
        {
            Console.WriteLine("[MetricsLogger Process] Starting...");
            Console.WriteLine("[MetricsLogger Process] PID: " + Environment.ProcessId);

            _pipeName = GetPipeNameFromArgs(args);
            if (string.IsNullOrEmpty(_pipeName))
            {
                Console.WriteLine("[MetricsLogger Process] ERROR: No pipe name specified.  Use --pipe <name>");
                Console.WriteLine("[MetricsLogger Process] Running in standalone mode for testing...");
                RunStandaloneMode();
                return;
            }

            Console.WriteLine("[MetricsLogger Process] Connecting to pipe: " + _pipeName);

            SetupLogFile();

            try
            {
                ConnectAndListen();
            }
            catch (Exception ex)
            {
                Console.WriteLine("[MetricsLogger Process] Fatal error: " + ex.Message);
                LogToFile("[FATAL] " + ex.Message);
            }

            Console.WriteLine("[MetricsLogger Process] Shutting down...");
            LogToFile("========================================");
            LogToFile("MetricsLogger Process Stopped: " + DateTime.Now. ToString("yyyy-MM-dd HH:mm:ss"));
            LogToFile("Total events received: " + _eventsReceived);
            LogToFile("UserLoggedInEvent count: " + _userLoginsLogged);
            LogToFile("DataProcessedEvent count: " + _dataProcessedLogged);
            LogToFile("========================================");
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

        private static void SetupLogFile()
        {
            string logsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ". .", ". .", ". .", ". .", ". .", "Logs");
            try
            {
                logsDir = Path. GetFullPath(logsDir);
            }
            catch
            {
                logsDir = Path. Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            }

            if (! Directory.Exists(logsDir))
            {
                try
                {
                    Directory.CreateDirectory(logsDir);
                }
                catch
                {
                    logsDir = Path. Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                    if (!Directory.Exists(logsDir))
                    {
                        Directory.CreateDirectory(logsDir);
                    }
                }
            }

            _logFilePath = Path. Combine(logsDir, "metrics_" + DateTime.Now. ToString("yyyy-MM-dd") + ".log");

            LogToFile("========================================");
            LogToFile("MetricsLogger Process Started: " + DateTime. Now.ToString("yyyy-MM-dd HH:mm:ss"));
            LogToFile("PID: " + Environment.ProcessId);
            LogToFile("IPC: Named Pipes");
            LogToFile("========================================");

            Console.WriteLine("[MetricsLogger Process] Log file: " + _logFilePath);
        }

        private static void ConnectAndListen()
        {
            using (var pipeClient = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous))
            {
                Console.WriteLine("[MetricsLogger Process] Connecting to kernel...");
                pipeClient.Connect(30000); // 30 second timeout
                Console.WriteLine("[MetricsLogger Process] Connected to kernel!");

                // Send ready message
                SendMessage(pipeClient, new IpcMessage
                {
                    Type = IpcMessageType. Ack,
                    PluginName = "MetricsLogger",
                    Response = "Ready"
                });

                // Start heartbeat thread
                var heartbeatThread = new Thread(() => SendHeartbeats(pipeClient));
                heartbeatThread.IsBackground = true;
                heartbeatThread. Start();

                // Listen for messages from kernel
                while (_running && pipeClient.IsConnected)
                {
                    try
                    {
                        var message = IpcProtocol.ReadMessage(pipeClient);
                        if (message == null)
                        {
                            Thread.Sleep(10);
                            continue;
                        }

                        HandleMessage(pipeClient, message);
                    }
                    catch (IOException)
                    {
                        Console.WriteLine("[MetricsLogger Process] Pipe disconnected.");
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[MetricsLogger Process] Error: " + ex.Message);
                    }
                }
            }
        }

        private static void SendHeartbeats(NamedPipeClientStream pipe)
        {
            while (_running && pipe.IsConnected)
            {
                try
                {
                    Thread.Sleep(5000);
                    if (pipe.IsConnected)
                    {
                        SendMessage(pipe, new IpcMessage
                        {
                            Type = IpcMessageType.Heartbeat,
                            PluginName = "MetricsLogger"
                        });
                    }
                }
                catch
                {
                    break;
                }
            }
        }

        private static void HandleMessage(NamedPipeClientStream pipe, IpcMessage message)
        {
            switch (message.Type)
            {
                case IpcMessageType. Event:
                    HandleEvent(message. Event);
                    break;

                case IpcMessageType.Start:
                    Console.WriteLine("[MetricsLogger Process] Received START command.");
                    LogToFile("[COMMAND] Start received");
                    break;

                case IpcMessageType.Stop:
                    Console.WriteLine("[MetricsLogger Process] Received STOP command.");
                    LogToFile("[COMMAND] Stop received");
                    break;

                case IpcMessageType.Shutdown:
                    Console.WriteLine("[MetricsLogger Process] Received SHUTDOWN command.");
                    LogToFile("[COMMAND] Shutdown received");
                    _running = false;
                    break;

                default:
                    Console.WriteLine("[MetricsLogger Process] Unknown message type: " + message.Type);
                    break;
            }
        }

        private static void HandleEvent(EventMessage evt)
        {
            if (evt == null) return;

            _eventsReceived++;

            string topic = evt.Topic ??  "";
            string payload = evt.Payload ?? "";
            DateTime timestamp = evt.Timestamp != default ? evt.Timestamp : DateTime.Now;

            string logLine;
            string consoleMessage;

            // Handle UserLoggedInEvent (REQUIRED by assignment)
            if (topic. Equals("UserLoggedInEvent", StringComparison.OrdinalIgnoreCase))
            {
                _userLoginsLogged++;
                var userEvent = Deserialize<UserLoggedInEvent>(payload);
                if (userEvent != null)
                {
                    logLine = "[" + timestamp. ToString("yyyy-MM-dd HH:mm:ss") + "] [UserLoggedInEvent] User: " + userEvent. Username + " (ID: " + userEvent.UserId + "), IP: " + userEvent.IpAddress + ", Session: " + userEvent.SessionId;
                    consoleMessage = "[UserLoggedInEvent] User '" + userEvent. Username + "' logged in from " + userEvent. IpAddress;
                }
                else
                {
                    logLine = "[" + timestamp. ToString("yyyy-MM-dd HH:mm:ss") + "] [UserLoggedInEvent] " + payload;
                    consoleMessage = "[UserLoggedInEvent] " + payload;
                }
            }
            // Handle DataProcessedEvent (REQUIRED by assignment)
            else if (topic.Equals("DataProcessedEvent", StringComparison.OrdinalIgnoreCase))
            {
                _dataProcessedLogged++;
                var dataEvent = Deserialize<DataProcessedEvent>(payload);
                if (dataEvent != null)
                {
                    string status = dataEvent.Success ? "SUCCESS" : "FAILED";
                    logLine = "[" + timestamp.ToString("yyyy-MM-dd HH:mm:ss") + "] [DataProcessedEvent] [" + status + "] Source: " + dataEvent.DataSource + ", Records: " + dataEvent.RecordsProcessed + ", Time: " + dataEvent.ProcessingTimeMs. ToString("F2") + "ms";
                    if (dataEvent.Success)
                    {
                        consoleMessage = "[DataProcessedEvent] SUCCESS: " + dataEvent.RecordsProcessed + " records from " + dataEvent. DataSource + " (" + dataEvent. ProcessingTimeMs. ToString("F2") + "ms)";
                    }
                    else
                    {
                        consoleMessage = "[DataProcessedEvent] FAILED: " + dataEvent.DataSource + " - " + dataEvent. ErrorMessage;
                    }
                }
                else
                {
                    logLine = "[" + timestamp.ToString("yyyy-MM-dd HH:mm:ss") + "] [DataProcessedEvent] " + payload;
                    consoleMessage = "[DataProcessedEvent] " + payload;
                }
            }
            // Handle metrics
            else if (topic.StartsWith("metrics", StringComparison. OrdinalIgnoreCase))
            {
                logLine = "[" + timestamp.ToString("yyyy-MM-dd HH:mm:ss") + "] [METRIC] " + topic + ": " + payload;
                consoleMessage = "[METRIC] " + topic + ": " + payload;
            }
            // Handle other events
            else
            {
                logLine = "[" + timestamp.ToString("yyyy-MM-dd HH:mm:ss") + "] [EVENT] " + topic + ": " + payload;
                consoleMessage = "[EVENT] " + topic + ": " + payload;
            }

            LogToFile(logLine);
            Console. WriteLine("[MetricsLogger Process] " + consoleMessage);
        }

        private static T Deserialize<T>(string json) where T : class
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                return JsonSerializer. Deserialize<T>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch
            {
                return null;
            }
        }

        private static void SendMessage(NamedPipeClientStream pipe, IpcMessage message)
        {
            try
            {
                IpcProtocol. WriteMessage(pipe, message);
            }
            catch (Exception ex)
            {
                Console. WriteLine("[MetricsLogger Process] SendMessage error: " + ex.Message);
            }
        }

        private static void LogToFile(string content)
        {
            try
            {
                File.AppendAllText(_logFilePath, content + Environment.NewLine);
            }
            catch { }
        }

        private static void RunStandaloneMode()
        {
            SetupLogFile();
            Console.WriteLine("[MetricsLogger Process] Running in standalone test mode.");
            Console.WriteLine("[MetricsLogger Process] Press Enter to exit...");
            Console.ReadLine();
        }
    }
}