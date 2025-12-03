using System;
using System.  IO;
using System. IO. Pipes;
using System.  Text.  Json;
using Contracts;
using Contracts.IPC;

namespace MetricsLoggerProcess
{
    /// <summary>
    /// MetricsLogger plugin, receives events and logs them to a file.
    /// Runs as a separate process and communicates with the kernel via named pipes.
    /// </summary>
    class Program
    {
        // IPC connection state
        private static string _pipeName = "";
        private static NamedPipeClientStream _pipeClient = null;
        private static bool _running = true;
        
        // Logging state
        private static string _logFilePath = "";
        
        // Statistics tracking
        private static int _totalEventsReceived = 0;
        private static int _userLoginCount = 0;
        private static int _dataProcessedCount = 0;
        private static int _systemMetricsCount = 0;

        static void Main(string[] args)
        {
            Console. WriteLine("[MetricsLogger] Starting...");
            Console.WriteLine("[MetricsLogger] PID: " + Environment.ProcessId);

            // Get pipe name from command line arguments (passed by kernel)
            _pipeName = GetPipeNameFromArgs(args);
            if (string. IsNullOrEmpty(_pipeName))
            {
                Console.WriteLine("[MetricsLogger] ERROR: No pipe name specified.   Use --pipe <name>");
                return;
            }

            try
            {
                ConnectAndListen();
            }
            catch (Exception ex)
            {
                Console.  WriteLine("[MetricsLogger] Fatal error: " + ex.Message);
            }

            // Print final statistics on shutdown
            Console.WriteLine("[MetricsLogger] Shutting down...");
            Console. WriteLine("[MetricsLogger] Total events: " + _totalEventsReceived);
            Console.WriteLine("[MetricsLogger] User logins: " + _userLoginCount);
            Console.WriteLine("[MetricsLogger] Data processed: " + _dataProcessedCount);
            Console.  WriteLine("[MetricsLogger] System metrics: " + _systemMetricsCount);
        }

        /// <summary>
        /// Sets up the log file in the Logs directory.
        /// Creates the directory if it doesn't exist.
        /// </summary>
        private static void SetupLogFile()
        {
            try
            {
                // Navigate up from bin output to project root, then to Logs folder
                string logDir = Path.Combine("..", "..", "..", "..", "Logs");
                logDir = Path.GetFullPath(logDir);

                Console.WriteLine("[MetricsLogger] Log directory: " + logDir);

                if (!Directory.  Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                    Console.WriteLine("[MetricsLogger] Created log directory");
                }

                // Create log file with date in filename
                string fileName = "metrics_" + DateTime. Now.ToString("yyyy-MM-dd") + ".log";
                _logFilePath = Path.  Combine(logDir, fileName);
                Console.WriteLine("[MetricsLogger] Log file path: " + _logFilePath);

                // Write startup marker
                using (StreamWriter sw = new StreamWriter(_logFilePath, true))
                {
                    sw.WriteLine("[" + DateTime.Now.  ToString("HH:mm:ss.fff") + "] MetricsLogger started");
                    sw.Flush();
                }

                if (File.Exists(_logFilePath))
                {
                    Console.WriteLine("[MetricsLogger] SUCCESS: Log file created");
                }
            }
            catch (Exception ex)
            {
                Console.  WriteLine("[MetricsLogger] ERROR setting up log file: " + ex.Message);
                _logFilePath = "";
            }
        }

        /// <summary>
        /// Extracts the pipe name from command line arguments. 
        /// </summary>
        private static string GetPipeNameFromArgs(string[] args)
        {
            for (int i = 0; i < args. Length - 1; i++)
            {
                if (args[i] == "--pipe")
                {
                    return args[i + 1];
                }
            }
            return "";
        }

        /// <summary>
        /// Connects to the kernel's named pipe and enters the message loop.
        /// </summary>
        private static void ConnectAndListen()
        {
            Console.WriteLine("[MetricsLogger] Connecting to pipe: " + _pipeName);

            _pipeClient = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut);
            _pipeClient.Connect(5000);
            Console.WriteLine("[MetricsLogger] Connected to kernel.");

            // Send ready acknowledgment
            IpcProtocol.  WriteMessage(_pipeClient, new IpcMessage
            {
                Type = IpcMessageType. Ack,
                PluginName = "MetricsLoggerProcess",
                Response = "Ready"
            });

            // Setup logging after connection established
            Console.WriteLine("[MetricsLogger] About to setup log file...");
            SetupLogFile();
            Console.WriteLine("[MetricsLogger] Log file setup complete. Path: " + _logFilePath);

            // Main message loop
            while (_running && _pipeClient. IsConnected)
            {
                try
                {
                    var message = IpcProtocol.  ReadMessage(_pipeClient);
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
                    Console.WriteLine("[MetricsLogger] Error reading pipe: " + ex.  Message);
                    break;
                }
            }
        }

        /// <summary>
        /// Handles messages received from the kernel. 
        /// </summary>
        private static void HandleMessage(IpcMessage message)
        {
            Console.WriteLine("[MetricsLogger] HandleMessage: " + message.Type);

            switch (message.  Type)
            {
                case IpcMessageType.Start:
                    Console. WriteLine("[MetricsLogger] Received START command.");
                    LogToFile("MetricsLogger received START");
                    SendAck("Started");
                    break;

                case IpcMessageType.Stop:
                    Console.WriteLine("[MetricsLogger] Received STOP command.");
                    SendAck("Stopping");
                    break;

                case IpcMessageType.  Shutdown:
                    Console.WriteLine("[MetricsLogger] Received SHUTDOWN command.");
                    SendAck("Shutting down");
                    PerformGracefulShutdown();
                    _running = false;
                    break;

                case IpcMessageType.  Event:
                    Console.WriteLine("[MetricsLogger] Received EVENT: " + (message. Event?. Topic ?? "null"));
                    HandleEvent(message.Event);
                    break;
            }
        }
        
        /// <summary>
        /// Sends an acknowledgment message to the kernel. 
        /// </summary>
        private static void SendAck(string response)
        {
            try
            {
                if (_pipeClient != null && _pipeClient.  IsConnected)
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
                Console. WriteLine("[MetricsLogger] Failed to send ACK: " + ex.Message);
            }
        }
        
        /// <summary>
        /// Sends an error message to the kernel. 
        /// </summary>
        private static void SendError(string errorMessage, int errorCode = 0)
        {
            try
            {
                if (_pipeClient != null && _pipeClient.  IsConnected)
                {
                    IpcProtocol. WriteMessage(_pipeClient, IpcMessage. CreateError(
                        "MetricsLoggerProcess",
                        errorMessage,
                        errorCode
                    ));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[MetricsLogger] Failed to send error: " + ex. Message);
            }
        }

        /// <summary>
        /// Performs graceful shutdown - logs final statistics. 
        /// </summary>
        private static void PerformGracefulShutdown()
        {
            Console.WriteLine("[MetricsLogger] Performing graceful shutdown...");

            // Log final statistics to file
            LogToFile("========================================");
            LogToFile("MetricsLogger Shutdown: " + DateTime.Now.  ToString("yyyy-MM-dd HH:mm:ss"));
            LogToFile("Session Statistics:");
            LogToFile("  Total events received: " + _totalEventsReceived);
            LogToFile("  User logins logged: " + _userLoginCount);
            LogToFile("  Data processed logged: " + _dataProcessedCount);
            LogToFile("  System metrics logged: " + _systemMetricsCount);
            LogToFile("========================================");

            Console.WriteLine("[MetricsLogger] Graceful shutdown complete.");
        }

        /// <summary>
        /// Handles events received from the kernel.
        /// Routes to appropriate handler based on topic.
        /// </summary>
        private static void HandleEvent(EventMessage evt)
        {
            if (evt == null) return;

            _totalEventsReceived++;
            string topic = evt.Topic ??   "";
            string payload = evt.  Payload ??  "";

            Console.WriteLine("[MetricsLogger] RECEIVED: " + topic);

            // Route to appropriate handler based on topic
            if (topic.  Equals(EventTopics.UserLoggedIn, StringComparison. OrdinalIgnoreCase))
            {
                HandleUserLoggedInEvent(payload);
            }
            else if (topic. Equals(EventTopics.DataProcessed, StringComparison.  OrdinalIgnoreCase))
            {
                HandleDataProcessedEvent(payload);
            }
            else if (topic.  Equals(EventTopics.SystemMetrics, StringComparison.OrdinalIgnoreCase))
            {
                HandleSystemMetricsEvent(payload);
            }
            else
            {
                // Log unknown events generically
                LogToFile("EVENT | Topic: " + topic + " | Payload: " + payload);
            }
        }

        /// <summary>
        /// Handles UserLoggedInEvent - parses JSON and logs formatted output.
        /// </summary>
        private static void HandleUserLoggedInEvent(string payload)
        {
            _userLoginCount++;
            try
            {
                var data = JsonSerializer. Deserialize<JsonElement>(payload);
                string user = data. TryGetProperty("username", out var u) ? u.GetString() : "unknown";
                string userId = data.TryGetProperty("userId", out var id) ? id.GetString() : "";
                string ip = data.TryGetProperty("ipAddress", out var ipProp) ? ipProp.GetString() : "";

                string logLine = "USER_LOGIN | User: " + user + " | UserId: " + userId + " | IP: " + ip;
                Console.WriteLine("[MetricsLogger] " + logLine);
                LogToFile(logLine);
            }
            catch (JsonException ex)
            {
                Console.  WriteLine("[MetricsLogger] Error parsing UserLoggedInEvent: " + ex.Message);
                LogToFile("USER_LOGIN | ERROR: Failed to parse payload");
                SendError("Failed to parse UserLoggedInEvent: " + ex.Message, 101);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[MetricsLogger] Error handling UserLoggedInEvent: " + ex.Message);
                SendError("Error handling UserLoggedInEvent: " + ex.Message, 100);
            }
        }

        /// <summary>
        /// Handles DataProcessedEvent - parses JSON and logs formatted output.
        /// </summary>
        private static void HandleDataProcessedEvent(string payload)
        {
            _dataProcessedCount++;
            try
            {
                var data = JsonSerializer.  Deserialize<JsonElement>(payload);
                string source = data.TryGetProperty("dataSource", out var s) ? s.GetString() : "unknown";
                int records = data.TryGetProperty("recordsProcessed", out var r) ? r.  GetInt32() : 0;
                string status = data.TryGetProperty("success", out var success) && success.GetBoolean() ? "SUCCESS" : "FAILED";
                double time = data.TryGetProperty("processingTimeMs", out var t) ? t.GetDouble() : 0;

                string logLine = "DATA_PROCESSED | Source: " + source + " | Records: " + records + 
                                 " | Status: " + status + " | Time: " + time.  ToString("F2") + "ms";

                // Include error message if processing failed
                if (status == "FAILED" && data.TryGetProperty("errorMessage", out var err))
                {
                    logLine += " | Error: " + err.  GetString();
                }

                Console.  WriteLine("[MetricsLogger] " + logLine);
                LogToFile(logLine);
            }
            catch (JsonException ex)
            {
                Console. WriteLine("[MetricsLogger] Error parsing DataProcessedEvent: " + ex.Message);
                LogToFile("DATA_PROCESSED | ERROR: Failed to parse payload");
                SendError("Failed to parse DataProcessedEvent: " + ex. Message, 102);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[MetricsLogger] Error handling DataProcessedEvent: " + ex. Message);
                SendError("Error handling DataProcessedEvent: " + ex.Message, 100);
            }
        }

        /// <summary>
        /// Handles SystemMetricsEvent, parses JSON and logs formatted output. 
        /// </summary>
        private static void HandleSystemMetricsEvent(string payload)
        {
            _systemMetricsCount++;
            try
            {
                var data = JsonSerializer.  Deserialize<JsonElement>(payload);
                string machine = data.TryGetProperty("machineName", out var m) ? m.GetString() : Environment.MachineName;
                double cpu = data.TryGetProperty("cpuUsagePercent", out var c) ? c.  GetDouble() : 0;
                double ram = data.TryGetProperty("memoryUsagePercent", out var r) ?  r. GetDouble() : 0;
                double disk = data.TryGetProperty("diskUsagePercent", out var d) ?  d.GetDouble() : 0;

                string logLine = "SYSTEM_METRICS | Machine: " + machine + 
                                 " | CPU: " + cpu.ToString("F1") + "%" + 
                                 " | RAM: " + ram.ToString("F1") + "%" + 
                                 " | Disk: " + disk. ToString("F1") + "%";
                Console. WriteLine("[MetricsLogger] " + logLine);
                LogToFile(logLine);
            }
            catch (JsonException ex)
            {
                Console.  WriteLine("[MetricsLogger] Error parsing SystemMetricsEvent: " + ex.Message);
                LogToFile("SYSTEM_METRICS | ERROR: Failed to parse payload");
                SendError("Failed to parse SystemMetricsEvent: " + ex.Message, 103);
            }
            catch (Exception ex)
            {
                Console.  WriteLine("[MetricsLogger] Error handling SystemMetricsEvent: " + ex.Message);
                SendError("Error handling SystemMetricsEvent: " + ex.Message, 100);
            }
        }

        /// <summary>
        /// Writes a message to the log file with timestamp.
        /// </summary>
        private static void LogToFile(string message)
        {
            if (string.IsNullOrEmpty(_logFilePath))
            {
                Console.WriteLine("[MetricsLogger] WARNING: Log file not configured");
                return;
            }

            try
            {
                string timestamp = DateTime. Now.ToString("yyyy-MM-dd HH:mm:ss. fff");
                string line = "[" + timestamp + "] " + message;

                using (StreamWriter sw = new StreamWriter(_logFilePath, true))
                {
                    sw. WriteLine(line);
                    sw.Flush();
                }

                Console.WriteLine("[MetricsLogger] LOGGED TO FILE: " + message);
            }
            catch (IOException ex)
            {
                Console.WriteLine("[MetricsLogger] ERROR writing to log: " + ex.Message);
                SendError("Failed to write to log file: " + ex.  Message, 200);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[MetricsLogger] ERROR writing to log: " + ex.Message);
                SendError("Log file error: " + ex.Message, 201);
            }
        }
    }
}