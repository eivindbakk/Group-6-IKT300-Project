using System;
using System. IO;
using System. Text.Json;
using Contracts;
using Contracts. Events;

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
        public string Description => "Listens for UserLoggedInEvent and DataProcessedEvent, logs to console and file. ";

        public PluginHelpInfo GetHelp()
        {
            var help = new PluginHelpInfo
            {
                DetailedDescription =
                    "The MetricsLogger plugin receives events published to the message bus " +
                    "and logs them.  Specifically handles UserLoggedInEvent and DataProcessedEvent " +
                    "as required by the assignment specification."
            };

            help.HandledTopics.Add("UserLoggedInEvent - Logs user login with deserialized data");
            help.HandledTopics.Add("DataProcessedEvent - Logs data processing with deserialized data");
            help.HandledTopics.Add("metrics. * - Logged as [METRIC]");
            help. HandledTopics. Add("* - Any other topic logged as [EVENT]");

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
            string assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string assemblyDir = Path.GetDirectoryName(assemblyLocation);
            DirectoryInfo current = new DirectoryInfo(assemblyDir);
            DirectoryInfo projectRoot = current.Parent?. Parent?. Parent?. Parent?. Parent ??  current;

            string logsDirectory = Path.Combine(projectRoot. FullName, "Logs");
            if (!Directory.Exists(logsDirectory))
            {
                Directory.CreateDirectory(logsDirectory);
            }

            string logFileName = "metrics_" + DateTime.Now.ToString("yyyy-MM-dd") + ".log";
            _logFilePath = Path.Combine(logsDirectory, logFileName);

            WriteToFile("========================================");
            WriteToFile("MetricsLogger Started: " + DateTime.Now. ToString("yyyy-MM-dd HH:mm:ss"));
            WriteToFile("========================================");
        }

        public void Stop()
        {
            WriteToFile("========================================");
            WriteToFile("MetricsLogger Stopped: " + DateTime.Now. ToString("yyyy-MM-dd HH:mm:ss"));
            WriteToFile("Total events: " + _eventsReceived);
            WriteToFile("UserLoggedInEvent count: " + _userLoginsLogged);
            WriteToFile("DataProcessedEvent count: " + _dataProcessedLogged);
            WriteToFile("Metrics count: " + _metricsLogged);
            WriteToFile("========================================");

            _host?. Log($"MetricsLogger stopped. Events: {_eventsReceived}, Logins: {_userLoginsLogged}, DataProcessed: {_dataProcessedLogged}");
        }

        public void HandleEvent(EventMessage evt)
        {
            if (evt == null) return;

            _eventsReceived++;

            string topic = evt.Topic ??  "";
            string payload = evt. Payload ?? "";
            DateTime timestamp = evt. Timestamp != default ? evt.Timestamp : DateTime.Now;

            string logLine;
            string consoleMessage;

            // REQUIRED: Handle UserLoggedInEvent
            if (topic. Equals("UserLoggedInEvent", StringComparison.OrdinalIgnoreCase))
            {
                _userLoginsLogged++;
                var userEvent = DeserializePayload<UserLoggedInEvent>(payload);
                if (userEvent != null)
                {
                    logLine = $"[{timestamp:yyyy-MM-dd HH:mm:ss}] [UserLoggedInEvent] User: {userEvent.Username} (ID: {userEvent. UserId}), IP: {userEvent.IpAddress}, Session: {userEvent.SessionId}";
                    consoleMessage = $"[UserLoggedInEvent] User '{userEvent.Username}' logged in from {userEvent.IpAddress}";
                }
                else
                {
                    logLine = $"[{timestamp:yyyy-MM-dd HH:mm:ss}] [UserLoggedInEvent] {payload}";
                    consoleMessage = "[UserLoggedInEvent] " + payload;
                }
            }
            // REQUIRED: Handle DataProcessedEvent
            else if (topic.Equals("DataProcessedEvent", StringComparison. OrdinalIgnoreCase))
            {
                _dataProcessedLogged++;
                var dataEvent = DeserializePayload<DataProcessedEvent>(payload);
                if (dataEvent != null)
                {
                    string status = dataEvent.Success ? "SUCCESS" : "FAILED";
                    logLine = $"[{timestamp:yyyy-MM-dd HH:mm:ss}] [DataProcessedEvent] [{status}] Source: {dataEvent. DataSource}, Records: {dataEvent. RecordsProcessed}, Time: {dataEvent. ProcessingTimeMs:F2}ms";
                    if (dataEvent.Success)
                    {
                        consoleMessage = $"[DataProcessedEvent] SUCCESS: {dataEvent.RecordsProcessed} records from {dataEvent.DataSource} ({dataEvent.ProcessingTimeMs:F2}ms)";
                    }
                    else
                    {
                        consoleMessage = $"[DataProcessedEvent] FAILED: {dataEvent.DataSource} - {dataEvent.ErrorMessage}";
                    }
                }
                else
                {
                    logLine = $"[{timestamp:yyyy-MM-dd HH:mm:ss}] [DataProcessedEvent] {payload}";
                    consoleMessage = "[DataProcessedEvent] " + payload;
                }
            }
            // Handle metrics
            else if (topic.StartsWith("metrics", StringComparison. OrdinalIgnoreCase))
            {
                _metricsLogged++;
                logLine = $"[{timestamp:yyyy-MM-dd HH:mm:ss}] [METRIC] {topic}: {payload}";
                consoleMessage = $"[METRIC] {topic}: {payload}";
            }
            // Handle other events
            else
            {
                logLine = $"[{timestamp:yyyy-MM-dd HH:mm:ss}] [EVENT] {topic}: {payload}";
                consoleMessage = $"[EVENT] {topic}: {payload}";
            }

            WriteToFile(logLine);
            _host?.Log(consoleMessage);
        }

        private T DeserializePayload<T>(string payload) where T : class
        {
            if (string.IsNullOrWhiteSpace(payload)) return null;

            try
            {
                return JsonSerializer.Deserialize<T>(payload, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch
            {
                return null;
            }
        }

        private void WriteToFile(string content)
        {
            try
            {
                File.AppendAllText(_logFilePath, content + Environment.NewLine);
            }
            catch { }
        }
    }
}