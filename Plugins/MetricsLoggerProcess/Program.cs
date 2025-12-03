using System;
using System. IO;
using System. IO.Pipes;
using System. Text. Json;
using Contracts;
using Contracts.IPC;

namespace MetricsLoggerProcess
{
    class Program
    {
        private static string _pipeName = "";
        private static NamedPipeClientStream _pipeClient = null;
        private static bool _running = true;
        private static string _logFilePath = "";
        private static int _totalEventsReceived = 0;
        private static int _userLoginCount = 0;
        private static int _dataProcessedCount = 0;
        private static int _systemMetricsCount = 0;

        static void Main(string[] args)
        {
            Console.WriteLine("[MetricsLogger] Starting...");
            Console.WriteLine("[MetricsLogger] PID: " + Environment.ProcessId);

            _pipeName = GetPipeNameFromArgs(args);
            if (string.IsNullOrEmpty(_pipeName))
            {
                Console.WriteLine("[MetricsLogger] ERROR: No pipe name specified.  Use --pipe <name>");
                return;
            }

            try
            {
                ConnectAndListen();
            }
            catch (Exception ex)
            {
                Console. WriteLine("[MetricsLogger] Fatal error: " + ex.Message);
            }

            Console.WriteLine("[MetricsLogger] Shutting down...");
            Console.WriteLine("[MetricsLogger] Total events: " + _totalEventsReceived);
            Console.WriteLine("[MetricsLogger] User logins: " + _userLoginCount);
            Console.WriteLine("[MetricsLogger] Data processed: " + _dataProcessedCount);
            Console. WriteLine("[MetricsLogger] System metrics: " + _systemMetricsCount);
        }

        private static void SetupLogFile()
        {
            try
            {
                // Simple relative path - creates Logs folder next to where kernel runs from
                string logDir = Path.Combine("..", "..", "..", "..", "Logs");
                logDir = Path.GetFullPath(logDir);

                Console.WriteLine("[MetricsLogger] Log directory: " + logDir);

                if (!Directory. Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                    Console.WriteLine("[MetricsLogger] Created log directory");
                }

                string fileName = "metrics_" + DateTime.Now.ToString("yyyy-MM-dd") + ".log";
                _logFilePath = Path. Combine(logDir, fileName);
                Console.WriteLine("[MetricsLogger] Log file path: " + _logFilePath);

                using (StreamWriter sw = new StreamWriter(_logFilePath, true))
                {
                    sw.WriteLine("[" + DateTime.Now. ToString("HH:mm:ss. fff") + "] MetricsLogger started");
                    sw.Flush();
                }

                if (File.Exists(_logFilePath))
                {
                    Console.WriteLine("[MetricsLogger] SUCCESS: Log file created");
                }
            }
            catch (Exception ex)
            {
                Console. WriteLine("[MetricsLogger] ERROR setting up log file: " + ex.Message);
                _logFilePath = "";
            }
        }

        private static string GetPipeNameFromArgs(string[] args)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--pipe")
                {
                    return args[i + 1];
                }
            }
            return "";
        }

        private static void ConnectAndListen()
        {
            Console.WriteLine("[MetricsLogger] Connecting to pipe: " + _pipeName);

            _pipeClient = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut);
            _pipeClient.Connect(5000);
            Console.WriteLine("[MetricsLogger] Connected to kernel.");

            IpcProtocol. WriteMessage(_pipeClient, new IpcMessage
            {
                Type = IpcMessageType. Ack,
                PluginName = "MetricsLoggerProcess",
                Response = "Ready"
            });

            // Add this line to confirm we're about to setup logging
            Console.WriteLine("[MetricsLogger] About to setup log file.. .");
            SetupLogFile();
            Console.WriteLine("[MetricsLogger] Log file setup complete.  Path: " + _logFilePath);

            while (_running && _pipeClient.IsConnected)
            {
                try
                {
                    var message = IpcProtocol. ReadMessage(_pipeClient);
                    if (message != null)
                    {
                        HandleMessage(message);
                    }
                }
                catch (EndOfStreamException)
                {
                    Console.WriteLine("[MetricsLogger] Pipe closed.");
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[MetricsLogger] Error reading pipe: " + ex. Message);
                    break;
                }
            }
        }

        private static void HandleMessage(IpcMessage message)
        {
            Console.WriteLine("[MetricsLogger] HandleMessage: " + message.Type);

            switch (message. Type)
            {
                case IpcMessageType.Start:
                    Console.WriteLine("[MetricsLogger] Received START command.");
                    LogToFile("MetricsLogger received START");
                    SendAck("Started");
                    break;

                case IpcMessageType.Stop:
                    Console.WriteLine("[MetricsLogger] Received STOP command.");
                    SendAck("Stopping");
                    break;

                case IpcMessageType. Shutdown:
                    Console.WriteLine("[MetricsLogger] Received SHUTDOWN command.");
                    SendAck("Shutting down");
                    PerformGracefulShutdown();
                    _running = false;
                    break;

                case IpcMessageType. Event:
                    Console.WriteLine("[MetricsLogger] Received EVENT: " + (message.Event?.Topic ?? "null"));
                    HandleEvent(message.Event);
                    break;
            }
        }
        
        private static void SendAck(string response)
        {
            try
            {
                if (_pipeClient != null && _pipeClient. IsConnected)
                {
                    IpcProtocol. WriteMessage(_pipeClient, new IpcMessage
                    {
                        Type = IpcMessageType. Ack,
                        PluginName = "MetricsLoggerProcess",
                        Response = response
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[MetricsLogger] Failed to send ACK: " + ex.Message);
            }
        }
        
        private static void SendError(string errorMessage, int errorCode = 0)
        {
            try
            {
                if (_pipeClient != null && _pipeClient. IsConnected)
                {
                    IpcProtocol. WriteMessage(_pipeClient, IpcMessage.CreateError(
                        "MetricsLoggerProcess",
                        errorMessage,
                        errorCode
                    ));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[MetricsLogger] Failed to send error: " + ex.Message);
            }
        }

        private static void PerformGracefulShutdown()
        {
            Console.WriteLine("[MetricsLogger] Performing graceful shutdown...");

            // Log final statistics
            LogToFile("========================================");
            LogToFile("MetricsLogger Shutdown: " + DateTime.Now. ToString("yyyy-MM-dd HH:mm:ss"));
            LogToFile("Session Statistics:");
            LogToFile("  Total events received: " + _totalEventsReceived);
            LogToFile("  User logins logged: " + _userLoginCount);
            LogToFile("  Data processed logged: " + _dataProcessedCount);
            LogToFile("  System metrics logged: " + _systemMetricsCount);
            LogToFile("========================================");

            Console.WriteLine("[MetricsLogger] Graceful shutdown complete.");
        }

        private static void HandleEvent(EventMessage evt)
        {
            if (evt == null) return;

            _totalEventsReceived++;
            string topic = evt.Topic ??  "";
            string payload = evt. Payload ?? "";

            Console.WriteLine("[MetricsLogger] RECEIVED: " + topic);

            // Use constants for comparison
            if (topic. Equals(EventTopics.UserLoggedIn, StringComparison.OrdinalIgnoreCase))
            {
                HandleUserLoggedInEvent(payload);
            }
            else if (topic. Equals(EventTopics.DataProcessed, StringComparison. OrdinalIgnoreCase))
            {
                HandleDataProcessedEvent(payload);
            }
            else if (topic. Equals(EventTopics.SystemMetrics, StringComparison.OrdinalIgnoreCase))
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
                var data = JsonSerializer.Deserialize<JsonElement>(payload);
                string user = data.TryGetProperty("username", out var u) ? u.GetString() : "unknown";
                string userId = data.TryGetProperty("userId", out var id) ? id.GetString() : "";
                string ip = data.TryGetProperty("ipAddress", out var ipProp) ? ipProp.GetString() : "";

                string logLine = "USER_LOGIN | User: " + user + " | UserId: " + userId + " | IP: " + ip;
                Console.WriteLine("[MetricsLogger] " + logLine);
                LogToFile(logLine);
            }
            catch (JsonException ex)
            {
                Console. WriteLine("[MetricsLogger] Error parsing UserLoggedInEvent: " + ex.Message);
                LogToFile("USER_LOGIN | ERROR: Failed to parse payload");
                SendError("Failed to parse UserLoggedInEvent: " + ex.Message, 101);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[MetricsLogger] Error handling UserLoggedInEvent: " + ex.Message);
                SendError("Error handling UserLoggedInEvent: " + ex.Message, 100);
            }
        }

        private static void HandleDataProcessedEvent(string payload)
        {
            _dataProcessedCount++;
            try
            {
                var data = JsonSerializer. Deserialize<JsonElement>(payload);
                string source = data.TryGetProperty("dataSource", out var s) ? s.GetString() : "unknown";
                int records = data.TryGetProperty("recordsProcessed", out var r) ? r. GetInt32() : 0;
                string status = data.TryGetProperty("success", out var success) && success.GetBoolean() ? "SUCCESS" : "FAILED";
                double time = data.TryGetProperty("processingTimeMs", out var t) ? t.GetDouble() : 0;

                string logLine = "DATA_PROCESSED | Source: " + source + " | Records: " + records + 
                                 " | Status: " + status + " | Time: " + time. ToString("F2") + "ms";

                if (status == "FAILED" && data.TryGetProperty("errorMessage", out var err))
                {
                    logLine += " | Error: " + err. GetString();
                }

                Console. WriteLine("[MetricsLogger] " + logLine);
                LogToFile(logLine);
            }
            catch (JsonException ex)
            {
                Console.WriteLine("[MetricsLogger] Error parsing DataProcessedEvent: " + ex.Message);
                LogToFile("DATA_PROCESSED | ERROR: Failed to parse payload");
                SendError("Failed to parse DataProcessedEvent: " + ex.Message, 102);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[MetricsLogger] Error handling DataProcessedEvent: " + ex.Message);
                SendError("Error handling DataProcessedEvent: " + ex.Message, 100);
            }
        }

        private static void HandleSystemMetricsEvent(string payload)
        {
            _systemMetricsCount++;
            try
            {
                var data = JsonSerializer. Deserialize<JsonElement>(payload);
                string machine = data.TryGetProperty("machineName", out var m) ? m.GetString() : Environment.MachineName;
                double cpu = data.TryGetProperty("cpuUsagePercent", out var c) ? c. GetDouble() : 0;
                double ram = data.TryGetProperty("memoryUsagePercent", out var r) ? r.GetDouble() : 0;
                double disk = data.TryGetProperty("diskUsagePercent", out var d) ? d.GetDouble() : 0;

                string logLine = "SYSTEM_METRICS | Machine: " + machine + 
                                 " | CPU: " + cpu.ToString("F1") + "%" + 
                                 " | RAM: " + ram.ToString("F1") + "%" + 
                                 " | Disk: " + disk.ToString("F1") + "%";
                Console.WriteLine("[MetricsLogger] " + logLine);
                LogToFile(logLine);
            }
            catch (JsonException ex)
            {
                Console. WriteLine("[MetricsLogger] Error parsing SystemMetricsEvent: " + ex.Message);
                LogToFile("SYSTEM_METRICS | ERROR: Failed to parse payload");
                SendError("Failed to parse SystemMetricsEvent: " + ex.Message, 103);
            }
            catch (Exception ex)
            {
                Console. WriteLine("[MetricsLogger] Error handling SystemMetricsEvent: " + ex.Message);
                SendError("Error handling SystemMetricsEvent: " + ex.Message, 100);
            }
        }

        private static void LogToFile(string message)
        {
            if (string.IsNullOrEmpty(_logFilePath))
            {
                Console.WriteLine("[MetricsLogger] WARNING: Log file not configured");
                return;
            }

            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                string line = "[" + timestamp + "] " + message;

                using (StreamWriter sw = new StreamWriter(_logFilePath, true))
                {
                    sw.WriteLine(line);
                    sw.Flush();
                }

                Console.WriteLine("[MetricsLogger] LOGGED TO FILE: " + message);
            }
            catch (IOException ex)
            {
                Console.WriteLine("[MetricsLogger] ERROR writing to log: " + ex.Message);
                SendError("Failed to write to log file: " + ex. Message, 200);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[MetricsLogger] ERROR writing to log: " + ex.Message);
                SendError("Log file error: " + ex.Message, 201);
            }
        }
    }
}