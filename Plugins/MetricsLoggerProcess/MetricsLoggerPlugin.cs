using System;
using System. IO;
using Contracts;

namespace Plugins.MetricsLoggerProcess
{
    public class MetricsLoggerPlugin : IPlugin
    {
        private IPluginHost _host;
        private string _logFilePath;
        private int _eventsReceived;
        private int _metricsLogged;

        public string Name => "MetricsLogger";

        public Version Version => new Version(1, 0, 0);

        public string Description => "Listens for events and logs them to console and file.";

        public PluginHelpInfo GetHelp()
        {
            var help = new PluginHelpInfo
            {
                DetailedDescription =
                    "The MetricsLogger plugin receives all events published to the message bus " +
                    "and logs them both to the console and to a daily log file. Events are " +
                    "categorized by their topic prefix (metrics, log, alert, or other)."
            };

            help.HandledTopics.Add("metrics.*  - Logged as [METRIC]");
            help. HandledTopics. Add("log        - Logged as [LOG]");
            help.HandledTopics.Add("alert.*    - Logged as [ALERT]");
            help.HandledTopics.Add("*          - Any other topic logged as [EVENT]");

            help.Notes.Add("Log Location: Logs/metrics_YYYY-MM-DD. log");
            help. Notes.Add("A new log file is created each day.");
            help.Notes. Add("Events are appended to the log file.");

            help.Examples.Add("send metrics.cpu {\"value\": 75. 5}");
            help.Examples.Add("send metrics.memory {\"used\": 4096, \"total\": 8192}");
            help.Examples.Add("send log \"Application started\"");
            help. Examples.Add("send alert.warning \"High CPU usage\"");
            help.Examples. Add("send alert.critical \"Out of memory! \"");

            return help;
        }

        public void Start(IPluginHost host)
        {
            _host = host;
            _eventsReceived = 0;
            _metricsLogged = 0;

            SetupLogDirectory();

            _host.Log("MetricsLogger started. Log file: " + _logFilePath);
        }

        private void SetupLogDirectory()
        {
            string assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string assemblyDir = Path. GetDirectoryName(assemblyLocation);

            DirectoryInfo current = new DirectoryInfo(assemblyDir);
            DirectoryInfo projectRoot = current.Parent. Parent. Parent. Parent. Parent;

            string logsDirectory = Path.Combine(projectRoot. FullName, "Logs");

            if (!Directory. Exists(logsDirectory))
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
            WriteToFile("Events received: " + _eventsReceived);
            WriteToFile("Metrics logged: " + _metricsLogged);
            WriteToFile("========================================");

            if (_host != null)
            {
                _host.Log("MetricsLogger stopped. Events: " + _eventsReceived + ", Metrics: " + _metricsLogged);
            }
        }

        public void HandleEvent(EventMessage evt)
        {
            if (evt == null) return;

            // Hidden crash test: if we receive a crash event targeted at us, throw an exception
            if (evt.Topic != null && evt.Topic. Equals("plugin.crash", StringComparison.OrdinalIgnoreCase))
            {
                if (evt. Payload != null && evt.Payload. Equals(Name, StringComparison. OrdinalIgnoreCase))
                {
                    _host?.Log("MetricsLogger: Received crash command, simulating crash...");
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

            if (topic.StartsWith("metric", StringComparison. OrdinalIgnoreCase))
            {
                _metricsLogged++;
                logLine = string.Format("[{0:yyyy-MM-dd HH:mm:ss}] [METRIC] {1}: {2}", timestamp, topic, payload);
                consoleMessage = "[METRIC] " + topic + ": " + payload;
            }
            else if (topic. Equals("log", StringComparison.OrdinalIgnoreCase))
            {
                logLine = string.Format("[{0:yyyy-MM-dd HH:mm:ss}] [LOG] {1}", timestamp, payload);
                consoleMessage = "[LOG] " + payload;
            }
            else if (topic. StartsWith("alert", StringComparison.OrdinalIgnoreCase))
            {
                logLine = string.Format("[{0:yyyy-MM-dd HH:mm:ss}] [ALERT] {1}: {2}", timestamp, topic, payload);
                consoleMessage = "[ALERT] " + topic + ": " + payload;
            }
            else
            {
                logLine = string. Format("[{0:yyyy-MM-dd HH:mm:ss}] [EVENT] {1}: {2}", timestamp, topic, payload);
                consoleMessage = "[EVENT] " + topic + ": " + payload;
            }

            WriteToFile(logLine);

            if (_host != null)
            {
                _host. Log(consoleMessage);
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