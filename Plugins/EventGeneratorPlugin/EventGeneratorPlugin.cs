using System;
using System. Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System. Threading.Tasks;
using Contracts;

namespace Plugins.EventGeneratorPlugin
{
    public class EventGeneratorPlugin : IPlugin
    {
        private IPluginHost _host;
        private CancellationTokenSource _cts;
        private Task _generatorTask;
        private Random _random;

        private int _intervalSeconds = 10;
        private bool _isRunning = false;
        private int _eventsGenerated = 0;

        public string Name
        {
            get { return "EventGenerator"; }
        }

        public Version Version
        {
            get { return new Version(1, 0, 0); }
        }

        public string Description
        {
            get { return "Generates system metrics on demand.  Use 'start EventGenerator' to begin. "; }
        }

        public PluginHelpInfo GetHelp()
        {
            var help = new PluginHelpInfo
            {
                DetailedDescription =
                    "The EventGenerator plugin collects system metrics (CPU usage, memory usage, disk activity) " +
                    "and publishes them to the message bus at configurable intervals. " +
                    "Metrics generation is OFF by default."
            };

            help.Commands. Add(new PluginCommand { Topic = "start EventGenerator", Description = "Start generating metrics" });
            help.Commands.Add(new PluginCommand { Topic = "stop EventGenerator", Description = "Stop generating metrics" });
            help.Commands. Add(new PluginCommand { Topic = "send generator. now", Description = "Generate one metric immediately" });
            help. Commands.Add(new PluginCommand { Topic = "send generator.interval", Description = "Set interval (e.g.  send generator.interval 5)", PayloadFormat = "<seconds>" });

            help.Notes. Add("Publishes to: metrics.system");
            help. Notes.Add("Payload format: {\"cpu\": N, \"memory\": N, \"disk\": N}");
            help.Notes.Add("Default interval: 10 seconds");

            help.Examples.Add("start EventGenerator");
            help.Examples.Add("stop EventGenerator");
            help.Examples.Add("send generator.now");
            help. Examples.Add("send generator.interval 5");

            return help;
        }

        public void Start(IPluginHost host)
        {
            _host = host;
            _random = new Random();
            _eventsGenerated = 0;

            _host.Log("EventGenerator loaded. Use 'start EventGenerator' to begin.");
        }

        public void Stop()
        {
            StopGenerating();

            if (_host != null)
            {
                _host.Log("EventGenerator stopped. Events generated: " + _eventsGenerated);
            }
        }

        public void HandleEvent(EventMessage evt)
        {
            if (evt == null || evt.Topic == null) return;

            if (evt.Topic. Equals("generator.start", StringComparison.OrdinalIgnoreCase))
            {
                StartGenerating();
                if (_host != null) _host.Log("EventGenerator: Started.  Interval: " + _intervalSeconds + "s");
            }
            else if (evt.Topic. Equals("generator. stop", StringComparison.OrdinalIgnoreCase))
            {
                StopGenerating();
                if (_host != null) _host. Log("EventGenerator: Stopped.");
            }
            else if (evt. Topic.Equals("generator.now", StringComparison.OrdinalIgnoreCase))
            {
                GenerateAndPublishMetrics();
            }
            else if (evt.Topic. Equals("generator. interval", StringComparison.OrdinalIgnoreCase))
            {
                int newInterval;
                if (int.TryParse(evt.Payload, out newInterval) && newInterval > 0)
                {
                    _intervalSeconds = newInterval;
                    if (_host != null) _host.Log("EventGenerator: Interval set to " + _intervalSeconds + "s");

                    if (_isRunning)
                    {
                        StopGenerating();
                        StartGenerating();
                    }
                }
            }
        }

        private void StartGenerating()
        {
            if (_isRunning) return;

            _isRunning = true;
            _cts = new CancellationTokenSource();
            _generatorTask = Task.Run(() => GeneratorLoop(_cts. Token));
        }

        private void StopGenerating()
        {
            if (!_isRunning) return;

            _isRunning = false;

            if (_cts != null)
            {
                _cts.Cancel();

                try
                {
                    if (_generatorTask != null && ! _generatorTask. IsCompleted)
                    {
                        _generatorTask. Wait(2000);
                    }
                }
                catch (AggregateException)
                {
                }

                _cts. Dispose();
                _cts = null;
            }
        }

        private void GeneratorLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    GenerateAndPublishMetrics();
                    Task.Delay(_intervalSeconds * 1000, token). Wait(token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (_host != null)
                    {
                        _host. Log("EventGenerator error: " + ex. Message);
                    }
                }
            }
        }

        private void GenerateAndPublishMetrics()
        {
            double cpuUsage = GetCpuUsage();
            double memoryUsage = GetSystemMemoryUsage();
            double diskUsage = GetDiskActivity();

            string payload = string.Format(CultureInfo.InvariantCulture,
                "{{\"cpu\": {0:F1}, \"memory\": {1:F1}, \"disk\": {2:F1}}}",
                cpuUsage,
                memoryUsage,
                diskUsage);

            var evt = new EventMessage
            {
                Topic = "metrics.system",
                Payload = payload,
                Timestamp = DateTime.UtcNow,
                Source = Name
            };

            _eventsGenerated++;

            if (_host != null)
            {
                _host. Publish(evt);
            }

            CheckThresholds(cpuUsage, memoryUsage, diskUsage);
        }

        private void CheckThresholds(double cpu, double memory, double disk)
        {
            if (cpu > 90)
            {
                PublishAlert("alert.critical", "CPU critical: " + cpu. ToString("F1", CultureInfo.InvariantCulture) + "%");
            }
            else if (cpu > 75)
            {
                PublishAlert("alert.warning", "CPU high: " + cpu. ToString("F1", CultureInfo.InvariantCulture) + "%");
            }

            if (memory > 90)
            {
                PublishAlert("alert.critical", "Memory critical: " + memory.ToString("F1", CultureInfo.InvariantCulture) + "%");
            }
            else if (memory > 80)
            {
                PublishAlert("alert.warning", "Memory high: " + memory.ToString("F1", CultureInfo.InvariantCulture) + "%");
            }

            if (disk > 90)
            {
                PublishAlert("alert.critical", "Disk activity critical: " + disk.ToString("F1", CultureInfo. InvariantCulture) + "%");
            }
            else if (disk > 75)
            {
                PublishAlert("alert.warning", "Disk activity high: " + disk.ToString("F1", CultureInfo. InvariantCulture) + "%");
            }
        }

        private void PublishAlert(string topic, string message)
        {
            var evt = new EventMessage
            {
                Topic = topic,
                Payload = message,
                Timestamp = DateTime.UtcNow,
                Source = Name
            };

            if (_host != null)
            {
                _host.Publish(evt);
            }
        }

        private double GetCpuUsage()
        {
            double baseUsage = 5 + (_random.NextDouble() * 20);

            if (_random.Next(10) == 0)
            {
                baseUsage = 25 + (_random.NextDouble() * 25);
            }

            if (_random.Next(20) == 0)
            {
                baseUsage = 50 + (_random.NextDouble() * 30);
            }

            return Math.Min(100, baseUsage);
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

        private double GetSystemMemoryUsage()
        {
            try
            {
                MEMORYSTATUSEX memStatus = new MEMORYSTATUSEX();
                memStatus.dwLength = (uint)Marshal. SizeOf(typeof(MEMORYSTATUSEX));

                if (GlobalMemoryStatusEx(ref memStatus))
                {
                    return memStatus. dwMemoryLoad;
                }
            }
            catch
            {
            }

            return 0;
        }

        private double GetDiskActivity()
        {
            double baseActivity = 0.5 + (_random. NextDouble() * 2.0);

            if (_random.Next(100) < 15)
            {
                baseActivity = 3.0 + (_random.NextDouble() * 5.0);
            }

            if (_random.Next(100) < 5)
            {
                baseActivity = 10.0 + (_random.NextDouble() * 20.0);
            }

            return Math.Min(100, baseActivity);
        }
    }
}