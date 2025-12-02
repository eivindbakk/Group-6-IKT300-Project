using System;
using System.IO;
using System. Text. Json;
using Contracts;
using Contracts.Events;

namespace Plugins.MetricsLoggerProcess
{
    public class MetricsLoggerPlugin : IPlugin
    {
        private IPluginHost _host;
        private string _logFilePath;
        private int _eventsReceived;
        private int _metricsLogged;
        private int _userLoginsLogged;
        private int _dataProcessedLogged;

        public string Name => "MetricsLogger";

        public Version Version => new Version(1, 0, 0);

        public string Description => "Listens for events (including UserLoggedInEvent and DataProcessedEvent) and logs them to console and file. ";

        public PluginHelpInfo GetHelp()
        {
            var help = new PluginHelpInfo
            {
                DetailedDescription =
                    "The MetricsLogger plugin receives all events published to the message bus " +
                    "and logs them both to the console and to a daily log file. Events are " +
                    "categorized by their topic prefix.  Supports UserLoggedInEvent and DataProcessedEvent " +
                    "as specified in the assignment requirements."
            };

            help.HandledTopics. Add("user.loggedin  - Logged as [USER_LOGIN] with deserialized data");
            help.HandledTopics.Add("data.processed - Logged as [DATA_PROCESSED] with deserialized data");
            help.HandledTopics.Add("metrics.*      - Logged as [METRIC] with JSON parsing");
            help.HandledTopics.Add("log            - Logged as [LOG]");
            help.HandledTopics.Add("alert.*        - Logged as [ALERT]");
            help.HandledTopics.Add("*              - Any other topic logged as [EVENT]");

            help.Notes. Add("Log Location: Logs/metrics_YYYY-MM-DD. log");
            help.Notes.Add("A new log file is created each day.");
            help.Notes.Add("JSON payloads are properly deserialized and formatted.");

            help.Examples.Add("send user.loggedin {\"UserId\": \"123\", \"Username\": \"john\"}");
            help.Examples.Add("send data.processed {\"RecordsProcessed\": 100, \"Success\": true}");
            help.Examples.Add("send metrics.cpu {\"value\": 75. 5}");

            return help;
        }

        public void Start(IPluginHost host)
        {
            _host = host;
            _eventsReceived = 0;
            _metricsLogged = 0;
            _userLoginsLogged = 0;
            _dataProcessedLogged = 0;

            SetupLogDirectory();

            _host.Log("MetricsLogger started.  Log file: " + _logFilePath);
        }

        private void SetupLogDirectory()
        {
            string assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly(). Location;
            string assemblyDir = Path.GetDirectoryName(assemblyLocation);

            DirectoryInfo current = new DirectoryInfo(assemblyDir);
            DirectoryInfo projectRoot = current.Parent?. Parent?. Parent?. Parent?. Parent ??  current;

            string logsDirectory = Path.Combine(projectRoot. FullName, "Logs");

            if (!Directory. Exists(logsDirectory))
            {
                Directory.CreateDirectory(logsDirectory);
            }

            string logFileName = "metrics_" + DateTime.Now. ToString("yyyy-MM-dd") + ".log";
            _logFilePath = Path.Combine(logsDirectory, logFileName);

            WriteToFile("========================================");
            WriteToFile("MetricsLogger Started: " + DateTime.Now. ToString("yyyy-MM-dd HH:mm:ss"));
            WriteToFile("========================================");
        }

        public void Stop()
        {
            WriteToFile("========================================");
            WriteToFile("MetricsLogger Stopped: " + DateTime.Now. ToString("yyyy-MM-dd HH:mm:ss"));
            WriteToFile("Events received: " + _eventsReceived);
            WriteToFile("Metrics logged: " + _metricsLogged);
            WriteToFile("User logins logged: " + _userLoginsLogged);
            WriteToFile("Data processed events: " + _dataProcessedLogged);
            WriteToFile("========================================");

            if (_host != null)
            {
                _host.Log($"MetricsLogger stopped.  Events: {_eventsReceived}, Metrics: {_metricsLogged}, Logins: {_userLoginsLogged}, DataProcessed: {_dataProcessedLogged}");
            }
        }

        public void HandleEvent(EventMessage evt)
        {
            if (evt == null) return;

            // Hidden crash test
            if (evt. Topic != null && evt.Topic. Equals("plugin.crash", StringComparison.OrdinalIgnoreCase))
            {
                if (evt. Payload != null && evt.Payload. Equals(Name, StringComparison.OrdinalIgnoreCase))
                {
                    _host?. Log("MetricsLogger: Received crash command, simulating crash...");
                    throw new InvalidOperationException("CRASH TEST: MetricsLogger plugin crashed intentionally!");
                }
                return;
            }

            _eventsReceived++;

            string topic = evt.Topic ??  "";
            string payload = evt. Payload ?? "";
            DateTime timestamp = evt.Timestamp != default ? evt.Timestamp : DateTime.Now;

            string logLine;
            string consoleMessage;

            // Handle UserLoggedInEvent (as required by assignment)
            if (topic.Equals("user.loggedin", StringComparison.OrdinalIgnoreCase))
            {
                _userLoginsLogged++;
                var userEvent = DeserializePayload<UserLoggedInEvent>(payload);
                if (userEvent != null)
                {
                    logLine = string.Format("[{0:yyyy-MM-dd HH:mm:ss}] [USER_LOGIN] User: {1} (ID: {2}), IP: {3}, Session: {4}",
                        timestamp, userEvent.Username, userEvent.UserId, userEvent. IpAddress, userEvent.SessionId);
                    consoleMessage = $"[USER_LOGIN] {userEvent. Username} logged in from {userEvent.IpAddress}";
                }
                else
                {
                    logLine = string.Format("[{0:yyyy-MM-dd HH:mm:ss}] [USER_LOGIN] {1}", timestamp, payload);
                    consoleMessage = "[USER_LOGIN] " + payload;
                }
            }
            // Handle DataProcessedEvent (as required by assignment) - FIXED: removed extra space
            else if (topic.Equals("data.processed", StringComparison. OrdinalIgnoreCase))
            {
                _dataProcessedLogged++;
                var dataEvent = DeserializePayload<DataProcessedEvent>(payload);
                if (dataEvent != null)
                {
                    string successStatus = dataEvent.Success ? "SUCCESS" : "FAILED";
                    logLine = string.Format("[{0:yyyy-MM-dd HH:mm:ss}] [DATA_PROCESSED] [{1}] ProcessId: {2}, Source: {3}, Records: {4}, Time: {5:F2}ms",
                        timestamp, successStatus, dataEvent.ProcessId, dataEvent. DataSource, dataEvent. RecordsProcessed,
                        dataEvent.ProcessingTimeMs);
                    
                    if (dataEvent.Success)
                    {
                        consoleMessage = $"[DATA_PROCESSED] SUCCESS: {dataEvent. RecordsProcessed} records from {dataEvent.DataSource} ({dataEvent.ProcessingTimeMs:F2}ms)";
                    }
                    else
                    {
                        consoleMessage = $"[DATA_PROCESSED] FAILED: {dataEvent.RecordsProcessed} records from {dataEvent.DataSource} - {dataEvent.ErrorMessage}";
                    }
                }
                else
                {
                    logLine = string.Format("[{0:yyyy-MM-dd HH:mm:ss}] [DATA_PROCESSED] {1}", timestamp, payload);
                    consoleMessage = "[DATA_PROCESSED] " + payload;
                }
            }
            // Handle metrics with JSON deserialization
            else if (topic.StartsWith("metric", StringComparison. OrdinalIgnoreCase))
            {
                _metricsLogged++;
                var formattedPayload = FormatJsonPayload(payload);
                logLine = string.Format("[{0:yyyy-MM-dd HH:mm:ss}] [METRIC] {1}: {2}", timestamp, topic, formattedPayload);
                consoleMessage = "[METRIC] " + topic + ": " + formattedPayload;
            }
            else if (topic. Equals("log", StringComparison.OrdinalIgnoreCase))
            {
                logLine = string.Format("[{0:yyyy-MM-dd HH:mm:ss}] [LOG] {1}", timestamp, payload);
                consoleMessage = "[LOG] " + payload;
            }
            else if (topic.StartsWith("alert", StringComparison.OrdinalIgnoreCase))
            {
                logLine = string.Format("[{0:yyyy-MM-dd HH:mm:ss}] [ALERT] {1}: {2}", timestamp, topic, payload);
                consoleMessage = "[ALERT] " + topic + ": " + payload;
            }
            else
            {
                logLine = string.Format("[{0:yyyy-MM-dd HH:mm:ss}] [EVENT] {1}: {2}", timestamp, topic, payload);
                consoleMessage = "[EVENT] " + topic + ": " + payload;
            }

            WriteToFile(logLine);

            if (_host != null)
            {
                _host. Log(consoleMessage);
            }
        }

        /// <summary>
        /// Deserialize JSON payload to a typed event object. 
        /// </summary>
        private T DeserializePayload<T>(string payload) where T : class
        {
            if (string.IsNullOrWhiteSpace(payload)) return null;

            try
            {
                return JsonSerializer. Deserialize<T>(payload, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Format JSON payload for readable logging.
        /// </summary>
        private string FormatJsonPayload(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload)) return payload;

            try
            {
                using (var doc = JsonDocument.Parse(payload))
                {
                    var root = doc.RootElement;
                    var parts = new System.Collections.Generic.List<string>();

                    foreach (var prop in root.EnumerateObject())
                    {
                        parts.Add($"{prop.Name}={prop.Value}");
                    }

                    return string.Join(", ", parts);
                }
            }
            catch
            {
                return payload;
            }
        }

        private void WriteToFile(string content)
        {
            try
            {
                File.AppendAllText(_logFilePath, content + Environment.NewLine);
            }
            catch
            {
                // Silently ignore file write errors
            }
        }
    }
}