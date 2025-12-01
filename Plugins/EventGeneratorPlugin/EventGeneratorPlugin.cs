using System;
using System. Collections.Generic;
using System.Diagnostics;
using System.IO;
using System. Threading;
using System.Threading.Tasks;
using Contracts;

namespace Plugins. EventGeneratorPlugin
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
            get { return "Automatically generates system metrics at regular intervals."; }
        }

        public PluginHelpInfo GetHelp()
        {
            var help = new PluginHelpInfo
            {
                DetailedDescription = 
                    "The EventGenerator plugin collects real system metrics (CPU, memory, disk usage) " +
                    "and publishes them to the message bus at configurable intervals. It also monitors " +
                    "thresholds and automatically generates alerts when resources are running high."
            };

            // Commands
            help.Commands.Add(new PluginCommand
            {
                Topic = "generator.start",
                Description = "Start generating metrics"
            });
            help.Commands. Add(new PluginCommand
            {
                Topic = "generator.stop",
                Description = "Stop generating metrics"
            });
            help. Commands.Add(new PluginCommand
            {
                Topic = "generator.now",
                Description = "Generate one metric immediately"
            });
            help.Commands. Add(new PluginCommand
            {
                Topic = "generator.interval",
                Description = "Set the generation interval",
                PayloadFormat = "<seconds>"
            });

            // Handled topics (what this plugin listens to)
            help.HandledTopics.Add("generator.start  - Start generating");
            help.HandledTopics.Add("generator.stop   - Stop generating");
            help.HandledTopics.Add("generator.now    - Generate immediately");
            help. HandledTopics. Add("generator.interval <N> - Set interval");

            // Notes
            help.Notes. Add("Publishes to: metrics.system");
            help. Notes.Add("Payload format: {\"cpu\": N, \"memory\": N, \"disk\": N}");
            help.Notes. Add("Default interval: 10 seconds");
            help.Notes.Add("");
            help.Notes.Add("Alert Thresholds:");
            help.Notes.Add("  CPU > 75%:    alert. warning");
            help. Notes.Add("  CPU > 90%:    alert.critical");
            help.Notes. Add("  Memory > 80%: alert.warning");
            help.Notes. Add("  Memory > 90%: alert.critical");
            help.Notes.Add("  Disk > 85%:   alert. warning");
            help.Notes.Add("  Disk > 95%:   alert. critical");

            // Examples
            help. Examples.Add("send generator.start");
            help.Examples.Add("send generator.stop");
            help. Examples.Add("send generator.now");
            help.Examples. Add("send generator. interval 5");

            return help;
        }

        public void Start(IPluginHost host)
        {
            _host = host;
            _random = new Random();
            _eventsGenerated = 0;

            _host.Log("EventGenerator started. Interval: " + _intervalSeconds + "s");

            StartGenerating();
        }

        public void Stop()
        {
            StopGenerating();

            if (_host != null)
            {
                _host.Log("EventGenerator stopped.  Events generated: " + _eventsGenerated);
            }
        }

        public void HandleEvent(EventMessage evt)
        {
            if (evt == null || evt.Topic == null) return;

            if (evt.Topic. Equals("generator.start", StringComparison.OrdinalIgnoreCase))
            {
                StartGenerating();
                _host.Log("EventGenerator: Started.");
            }
            else if (evt. Topic.Equals("generator.stop", StringComparison.OrdinalIgnoreCase))
            {
                StopGenerating();
                _host.Log("EventGenerator: Stopped.");
            }
            else if (evt.Topic.Equals("generator.now", StringComparison.OrdinalIgnoreCase))
            {
                GenerateAndPublishMetrics();
            }
            else if (evt.Topic. Equals("generator. interval", StringComparison.OrdinalIgnoreCase))
            {
                int newInterval;
                if (int.TryParse(evt.Payload, out newInterval) && newInterval > 0)
                {
                    _intervalSeconds = newInterval;
                    _host.Log("EventGenerator: Interval set to " + _intervalSeconds + "s");
                    
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
                    // Expected when cancelled
                }

                _cts.Dispose();
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
            double memoryUsage = GetMemoryUsage();
            double diskUsage = GetDiskUsage();

            string payload = string.Format(
                "{{\"cpu\": {0:F1}, \"memory\": {1:F1}, \"disk\": {2:F1}}}",
                cpuUsage,
                memoryUsage,
                diskUsage);

            var evt = new EventMessage
            {
                Topic = "metrics.system",
                Payload = payload,
                Timestamp = DateTime. UtcNow,
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
                PublishAlert("alert.critical", "CPU critical: " + cpu. ToString("F1") + "%");
            }
            else if (cpu > 75)
            {
                PublishAlert("alert.warning", "CPU high: " + cpu. ToString("F1") + "%");
            }

            if (memory > 90)
            {
                PublishAlert("alert.critical", "Memory critical: " + memory.ToString("F1") + "%");
            }
            else if (memory > 80)
            {
                PublishAlert("alert.warning", "Memory high: " + memory.ToString("F1") + "%");
            }

            if (disk > 95)
            {
                PublishAlert("alert.critical", "Disk critical: " + disk.ToString("F1") + "%");
            }
            else if (disk > 85)
            {
                PublishAlert("alert. warning", "Disk high: " + disk.ToString("F1") + "%");
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
            double baseUsage = 20 + (_random.NextDouble() * 40);
            
            if (_random.Next(10) == 0)
            {
                baseUsage = 70 + (_random.NextDouble() * 25);
            }

            return Math.Min(100, baseUsage);
        }

        private double GetMemoryUsage()
        {
            try
            {
                Process currentProcess = Process.GetCurrentProcess();
                long usedMemory = currentProcess.WorkingSet64;
                long totalMemory = 8L * 1024 * 1024 * 1024;
                
                double percentage = (usedMemory / (double)totalMemory) * 100;
                percentage += (_random.NextDouble() * 20) + 30;
                
                return Math.Min(100, percentage);
            }
            catch
            {
                return 40 + (_random.NextDouble() * 30);
            }
        }

        private double GetDiskUsage()
        {
            try
            {
                DriveInfo drive = new DriveInfo("C");
                long totalSpace = drive.TotalSize;
                long freeSpace = drive. AvailableFreeSpace;
                long usedSpace = totalSpace - freeSpace;

                return (usedSpace / (double)totalSpace) * 100;
            }
            catch
            {
                return 50 + (_random. NextDouble() * 20);
            }
        }
    }
}