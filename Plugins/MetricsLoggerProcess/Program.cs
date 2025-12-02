using System;
using System. IO;
using System.IO.Pipes;
using System. Text.Json;
using System.Threading;
using Contracts;
using Contracts. Events;
using Contracts.IPC;

namespace MetricsLoggerProcess
{
    class Program
    {
        private static string _pipeName;
        private static NamedPipeClientStream _pipeClient;
        private static bool _running = true;
        private static int _userLoginCount = 0;
        private static int _dataProcessedCount = 0;
        private static int _systemMetricsCount = 0;
        private static int _totalEventsReceived = 0;
        private static string _logFilePath;
        private static readonly object _logLock = new object();

        static void Main(string[] args)
        {
            Console.WriteLine("[MetricsLogger] Starting...");
            Console.WriteLine("[MetricsLogger] PID: " + Environment.ProcessId);

            _pipeName = GetPipeNameFromArgs(args);
            if (string. IsNullOrEmpty(_pipeName))
            {
                Console.WriteLine("[MetricsLogger] ERROR: No pipe name specified.  Use --pipe <name>");
                return;
            }

            // Setup log file
            string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ". .", ". .", ". .", ". .", ". .", "Logs");
            try
            {
                Directory.CreateDirectory(logDir);
            }
            catch
            {
                logDir = AppDomain.CurrentDomain.BaseDirectory;
            }
            _logFilePath = Path.Combine(logDir, "metrics_" + DateTime.Now.ToString("yyyy-MM-dd") + ".log");
            Console.WriteLine("[MetricsLogger] Log file: " + _logFilePath);

            try
            {
                ConnectAndListen();
            }
            catch (Exception ex)
            {
                Console.WriteLine("[MetricsLogger] Fatal error: " + ex.Message);
            }

            Console.WriteLine("[MetricsLogger] Shutting down...");
            Console.WriteLine("[MetricsLogger] Total events: " + _totalEventsReceived);
            Console.WriteLine("[MetricsLogger] User logins: " + _userLoginCount);
            Console.WriteLine("[MetricsLogger] Data processed: " + _dataProcessedCount);
            Console. WriteLine("[MetricsLogger] System metrics: " + _systemMetricsCount);
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
                Console.WriteLine("[MetricsLogger] Connecting to kernel...");
                _pipeClient.Connect(30000);
                Console.WriteLine("[MetricsLogger] Connected!");

                SendMessage(new IpcMessage
                {
                    Type = IpcMessageType.Ack,
                    PluginName = "MetricsLogger",
                    Response = "Ready"
                });

                var heartbeatThread = new Thread(SendHeartbeats);
                heartbeatThread. IsBackground = true;
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
                        Console.WriteLine("[MetricsLogger] Pipe disconnected.");
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[MetricsLogger] Error: " + ex.Message);
                    }
                }
            }
        }

        private static void SendHeartbeats()
        {
            while (_running && _pipeClient != null && _pipeClient.IsConnected)
            {
                try
                {
                    Thread.Sleep(5000);
                    if (_pipeClient. IsConnected)
                    {
                        SendMessage(new IpcMessage
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

        private static void HandleMessage(IpcMessage message)
        {
            switch (message.Type)
            {
                case IpcMessageType.Start:
                    Console.WriteLine("[MetricsLogger] Received START command.");
                    LogToFile("MetricsLogger started");
                    break;

                case IpcMessageType.Stop:
                    Console. WriteLine("[MetricsLogger] Received STOP command.");
                    break;

                case IpcMessageType. Shutdown:
                    Console.WriteLine("[MetricsLogger] Received SHUTDOWN command.");
                    _running = false;
                    break;

                case IpcMessageType.Event:
                    HandleEvent(message.Event);
                    break;
            }
        }

        private static void HandleEvent(EventMessage evt)
        {
            if (evt == null) return;

            _totalEventsReceived++;
            string topic = evt.Topic ?? "";
            string payload = evt.Payload ??  "";

            Console. WriteLine("[MetricsLogger] Received event: " + topic);

            if (topic. Equals("UserLoggedInEvent", StringComparison.OrdinalIgnoreCase))
            {
                HandleUserLoggedInEvent(payload);
            }
            else if (topic.Equals("DataProcessedEvent", StringComparison.OrdinalIgnoreCase))
            {
                HandleDataProcessedEvent(payload);
            }
            else if (topic.Equals("SystemMetricsEvent", StringComparison. OrdinalIgnoreCase))
            {
                HandleSystemMetricsEvent(payload);
            }
            else
            {
                LogToFile("EVENT | Topic: " + topic + " | Payload: " + payload);
            }
        }

        private static void HandleUserLoggedInEvent(string payload)
        {
            _userLoginCount++;

            try
            {
                var userEvent = JsonSerializer.Deserialize<UserLoggedInEvent>(payload, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (userEvent != null)
                {
                    string logMessage = string.Format(
                        "USER_LOGIN | User: {0} | UserId: {1} | IP: {2}",
                        userEvent.Username,
                        userEvent.UserId,
                        userEvent.IpAddress);

                    Console. WriteLine("[MetricsLogger] " + logMessage);
                    LogToFile(logMessage);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[MetricsLogger] Error parsing UserLoggedInEvent: " + ex.Message);
                LogToFile("ERROR parsing UserLoggedInEvent: " + payload);
            }
        }

        private static void HandleDataProcessedEvent(string payload)
        {
            _dataProcessedCount++;

            try
            {
                var dataEvent = JsonSerializer.Deserialize<DataProcessedEvent>(payload, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (dataEvent != null)
                {
                    string status = dataEvent.Success ? "SUCCESS" : "FAILED";
                    string logMessage = string.Format(
                        "DATA_PROCESSED | Source: {0} | Records: {1} | Status: {2} | Time: {3:F2}ms",
                        dataEvent.DataSource,
                        dataEvent.RecordsProcessed,
                        status,
                        dataEvent.ProcessingTimeMs);

                    if (! dataEvent.Success && ! string.IsNullOrEmpty(dataEvent.ErrorMessage))
                    {
                        logMessage += " | Error: " + dataEvent.ErrorMessage;
                    }

                    Console. WriteLine("[MetricsLogger] " + logMessage);
                    LogToFile(logMessage);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[MetricsLogger] Error parsing DataProcessedEvent: " + ex. Message);
                LogToFile("ERROR parsing DataProcessedEvent: " + payload);
            }
        }

        private static void HandleSystemMetricsEvent(string payload)
        {
            _systemMetricsCount++;

            try
            {
                var metricsEvent = JsonSerializer.Deserialize<SystemMetricsEvent>(payload, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (metricsEvent != null)
                {
                    string logMessage = string.Format(
                        "SYSTEM_METRICS | Machine: {0} | CPU: {1:F1}% | RAM: {2:F1}% | Disk: {3:F1}%",
                        metricsEvent.MachineName,
                        metricsEvent.CpuUsagePercent,
                        metricsEvent.MemoryUsagePercent,
                        metricsEvent.DiskUsagePercent);

                    Console. WriteLine("[MetricsLogger] " + logMessage);
                    LogToFile(logMessage);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[MetricsLogger] Error parsing SystemMetricsEvent: " + ex.Message);
                LogToFile("ERROR parsing SystemMetricsEvent: " + payload);
            }
        }

        private static void LogToFile(string message)
        {
            try
            {
                lock (_logLock)
                {
                    string line = DateTime. UtcNow. ToString("yyyy-MM-dd HH:mm:ss.fff") + " | " + message;
                    File.AppendAllText(_logFilePath, line + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[MetricsLogger] Failed to write to log file: " + ex. Message);
            }
        }

        private static void SendMessage(IpcMessage message)
        {
            try
            {
                if (_pipeClient != null && _pipeClient.IsConnected)
                {
                    IpcProtocol.WriteMessage(_pipeClient, message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[MetricsLogger] SendMessage error: " + ex.Message);
            }
        }
    }
}